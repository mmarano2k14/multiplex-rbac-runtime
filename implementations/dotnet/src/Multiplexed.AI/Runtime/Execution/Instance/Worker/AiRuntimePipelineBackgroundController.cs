using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Tracing;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;

namespace Multiplexed.AI.Runtime.Execution.Instance.Worker
{
    /// <summary>
    /// Default implementation of <see cref="IAiRuntimePipelineBackgroundController"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The controller owns a bounded queue of pipeline run requests and processes
    /// them in the background.
    /// </para>
    /// <para>
    /// Each queued request creates one new runtime execution and therefore one
    /// distinct execution identifier.
    /// </para>
    /// <para>
    /// The controller does not reuse execution identifiers. The execution identifier
    /// remains the namespace for the execution record, DAG state, step states,
    /// retention artifacts, externalized payloads, resolver indexes, snapshots,
    /// and replay data.
    /// </para>
    /// <para>
    /// The controller limits the number of active pipeline runs through
    /// <see cref="AiRuntimePipelineBackgroundControllerOptions.MaxConcurrentRuns"/>.
    /// This is controller-level parallelism only. Distributed step-level concurrency
    /// remains controlled by the runtime concurrency engine and Redis gate.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimePipelineBackgroundController : IAiRuntimePipelineBackgroundController
    {
        private readonly AiDagExecutionEngine _engine;
        private readonly IAiRuntimeInstanceWorker _worker;
        private readonly IAiRuntimeInstanceWorkerGroup _workerGroup;
        private readonly IAiRuntimeInstanceWorkerFactory _workerFactory;
        private readonly IAiRuntimePipelineRunDefinitionResolver _definitionResolver;
        private readonly IAiRuntimePipelineRunDefinitionPublisher _definitionPublisher;
        private readonly IAiRuntimePipelineRunLifecycleHook _runLifecycleHook;
        private readonly IAiRuntimeLogger _logger;
        private readonly IAiRuntimeObservability _observability;

        private readonly AiRuntimePipelineBackgroundControllerOptions _options;
        private readonly Channel<AiRuntimeQueuedPipelineRun> _queue;
        private readonly SemaphoreSlim _parallelismGate;
        private readonly ConcurrentDictionary<string, Task> _activeRuns = new(StringComparer.Ordinal);
        private readonly object _sync = new();

        private CancellationTokenSource? _controllerCancellation;
        private Task? _controllerTask;
        private bool _started;
        private bool _stopped;

        private volatile bool _queuePaused;
        private string? _queuePauseReason;
        private string? _queuePauseRequestedBy;
        private DateTime? _queuePausedAtUtc;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiRuntimePipelineBackgroundController"/> class.
        /// </summary>
        /// <param name="engine">The DAG execution engine used to create executions.</param>
        /// <param name="worker">The runtime instance worker used to advance created executions.</param>
        /// <param name="workerGroup">The runtime instance worker group used for distributed multi-instance execution.</param>
        /// <param name="workerFactory">The runtime instance worker factory used to create distributed workers.</param>
        /// <param name="definitionResolver">The pipeline run definition resolver.</param>
        /// <param name="definitionPublisher">The pipeline run definition publisher.</param>
        /// <param name="runLifecycleHook">The pipeline run lifecycle hook.</param>
        /// <param name="logger">The runtime logger.</param>
        /// <param name="observability">The runtime observability facade.</param>
        /// <param name="options">The controller options.</param>
        public AiRuntimePipelineBackgroundController(
            AiDagExecutionEngine engine,
            IAiRuntimeInstanceWorker worker,
            IAiRuntimeInstanceWorkerGroup workerGroup,
            IAiRuntimeInstanceWorkerFactory workerFactory,
            IAiRuntimePipelineRunDefinitionResolver definitionResolver,
            IAiRuntimePipelineRunDefinitionPublisher definitionPublisher,
            IAiRuntimePipelineRunLifecycleHook runLifecycleHook,
            IAiRuntimeLogger logger,
            IAiRuntimeObservability observability,
            IOptions<AiRuntimePipelineBackgroundControllerOptions> options)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _workerGroup = workerGroup ?? throw new ArgumentNullException(nameof(workerGroup));
            _workerFactory = workerFactory ?? throw new ArgumentNullException(nameof(workerFactory));
            _definitionResolver = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
            _definitionPublisher = definitionPublisher ?? throw new ArgumentNullException(nameof(definitionPublisher));
            _runLifecycleHook = runLifecycleHook ?? throw new ArgumentNullException(nameof(runLifecycleHook));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _observability = observability ?? throw new ArgumentNullException(nameof(observability));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            var queueCapacity = Math.Max(1, _options.QueueCapacity);
            var maxConcurrentRuns = Math.Max(1, _options.MaxConcurrentRuns);

            _queue = Channel.CreateBounded<AiRuntimeQueuedPipelineRun>(
                new BoundedChannelOptions(queueCapacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = false
                });

            _parallelismGate = new SemaphoreSlim(
                maxConcurrentRuns,
                maxConcurrentRuns);
        }

        /// <inheritdoc />
        public Task StartAsync(
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (_started)
                {
                    return Task.CompletedTask;
                }

                if (_stopped)
                {
                    throw new InvalidOperationException(
                        "The runtime pipeline background controller cannot be restarted after it has been stopped.");
                }

                _controllerCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                _controllerTask = Task.Run(
                    () => RunControllerLoopAsync(_controllerCancellation.Token),
                    CancellationToken.None);

                _started = true;
            }

            _logger.Engine.LogInformation(
                $"[AI PIPELINE CONTROLLER] Started. MaxConcurrentRuns='{_options.MaxConcurrentRuns}', QueueCapacity='{_options.QueueCapacity}'.");

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task StopAsync(
            CancellationToken cancellationToken = default)
        {
            Task? controllerTask;

            lock (_sync)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
                _queue.Writer.TryComplete();
                _controllerCancellation?.Cancel();

                controllerTask = _controllerTask;
            }

            _logger.Engine.LogInformation(
                "[AI PIPELINE CONTROLLER] Stop requested.");

            if (controllerTask is not null)
            {
                try
                {
                    await controllerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when the controller is stopped through cancellation.
                }
            }

            if (!_activeRuns.IsEmpty)
            {
                try
                {
                    await Task.WhenAll(_activeRuns.Values).WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when active runs are cancelled during shutdown.
                }
            }

            _controllerCancellation?.Dispose();

            _logger.Engine.LogInformation(
                "[AI PIPELINE CONTROLLER] Stopped.");
        }

        /// <inheritdoc />
        public async ValueTask<AiRuntimeWorkerRunHandle> EnqueueAsync(
            AiRuntimePipelineRunRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.PipelineName);

            if (_options.RejectEnqueueWhenStopped && !_started)
            {
                throw new InvalidOperationException(
                    "The runtime pipeline background controller has not been started.");
            }

            if (_stopped)
            {
                throw new InvalidOperationException(
                    "The runtime pipeline background controller has been stopped and cannot accept new work.");
            }

            var runId = Guid.NewGuid().ToString("N");

            var completionSource = new TaskCompletionSource<AiExecutionRecord>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var handle = new AiRuntimeWorkerRunHandle(
                runId,
                completionSource.Task);

            var queuedRun = new AiRuntimeQueuedPipelineRun(
                request,
                handle,
                completionSource);

            await _queue.Writer.WriteAsync(
                queuedRun,
                cancellationToken).ConfigureAwait(false);

            _logger.Engine.LogInformation(
                $"[AI PIPELINE CONTROLLER] Run queued. RunId='{runId}', Pipeline='{request.PipelineName}'.");

            return handle;
        }

        /// <inheritdoc />
        public Task PauseQueueAsync(
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_stopped)
            {
                throw new InvalidOperationException(
                    "The runtime pipeline background controller has been stopped and cannot be paused.");
            }

            _queuePaused = true;
            _queuePauseReason = reason;
            _queuePauseRequestedBy = requestedBy;
            _queuePausedAtUtc = DateTime.UtcNow;

            _logger.Engine.LogInformation(
                $"[AI PIPELINE CONTROLLER] Queue paused. Reason='{reason}', RequestedBy='{requestedBy}'.");

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ResumeQueueAsync(
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_stopped)
            {
                throw new InvalidOperationException(
                    "The runtime pipeline background controller has been stopped and cannot be resumed.");
            }

            var pausedSince = _queuePausedAtUtc;

            _queuePaused = false;
            _queuePauseReason = null;
            _queuePauseRequestedBy = requestedBy;
            _queuePausedAtUtc = null;

            _logger.Engine.LogInformation(
                $"[AI PIPELINE CONTROLLER] Queue resumed. RequestedBy='{requestedBy}', PausedSinceUtc='{pausedSince:O}'.");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Runs the main background controller loop.
        /// </summary>
        /// <param name="cancellationToken">The controller cancellation token.</param>
        private async Task RunControllerLoopAsync(
            CancellationToken cancellationToken)
        {
            _logger.Engine.LogInformation(
                "[AI PIPELINE CONTROLLER] Background loop started.");

            try
            {
                await foreach (var queuedRun in _queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {

                    await WaitWhileQueuePausedAsync(
                        queuedRun,
                        cancellationToken)
                    .ConfigureAwait(false);

                    await _parallelismGate.WaitAsync(cancellationToken).ConfigureAwait(false);

                    var task = ProcessQueuedRunAsync(
                        queuedRun,
                        cancellationToken);

                    _activeRuns.TryAdd(
                        queuedRun.Handle.RunId,
                        task);

                    _ = task.ContinueWith(
                        completed =>
                        {
                            _activeRuns.TryRemove(
                                queuedRun.Handle.RunId,
                                out _);

                            _parallelismGate.Release();

                            if (completed.Exception is not null)
                            {
                                _logger.Engine.LogError(
                                    $"[AI PIPELINE CONTROLLER] Run task faulted unexpectedly. RunId='{queuedRun.Handle.RunId}', Error='{completed.Exception.GetBaseException().Message}'.");
                            }
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancelQueuedRuns();
            }
            finally
            {
                _logger.Engine.LogInformation(
                    "[AI PIPELINE CONTROLLER] Background loop stopped.");
            }
        }

        /// <summary>
        /// Processes one queued pipeline run.
        /// </summary>
        /// <param name="queuedRun">The queued pipeline run.</param>
        /// <param name="cancellationToken">The controller cancellation token.</param>
        private async Task ProcessQueuedRunAsync(
            AiRuntimeQueuedPipelineRun queuedRun,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(queuedRun);

            var handle = queuedRun.Handle;
            var request = queuedRun.Request;

            try
            {
                await _observability.Tracer.TraceExecutionAsync(
                    new AiExecutionTraceContext
                    {
                        ExecutionId = handle.RunId,
                        ExecutionMode = "Dag",
                        Status = "PipelineRunQueued",
                        WorkerId = "pipeline-background-controller"
                    },
                    async () =>
                    {
                        await ProcessQueuedRunCoreAsync(
                            queuedRun,
                            cancellationToken).ConfigureAwait(false);

                        return true;
                    }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                handle.MarkCancelled();

                queuedRun.CompletionSource.TrySetCanceled(
                    cancellationToken);

                _logger.Engine.LogInformation(
                    $"[AI PIPELINE CONTROLLER] Run cancelled. RunId='{handle.RunId}', Pipeline='{request.PipelineName}'.");
            }
            catch (Exception ex)
            {
                handle.MarkFailed();

                queuedRun.CompletionSource.TrySetException(ex);

                _logger.Engine.LogError(
                    $"[AI PIPELINE CONTROLLER] Run failed. RunId='{handle.RunId}', Pipeline='{request.PipelineName}', Error='{ex.Message}'.");
            }
        }

        /// <summary>
        /// Executes the core pipeline run flow: resolve definition, publish definition,
        /// create execution, and advance execution through the runtime worker.
        /// </summary>
        /// <param name="queuedRun">The queued pipeline run.</param>
        /// <param name="cancellationToken">The controller cancellation token.</param>
        private async Task ProcessQueuedRunCoreAsync(
            AiRuntimeQueuedPipelineRun queuedRun,
            CancellationToken cancellationToken)
        {
            var handle = queuedRun.Handle;
            var request = queuedRun.Request;

            handle.MarkCreatingExecution();

            _logger.Engine.LogInformation(
                $"[AI PIPELINE CONTROLLER] Creating execution. RunId='{handle.RunId}', Pipeline='{request.PipelineName}'.");

            var definition = await _definitionResolver.ResolveAsync(
                request,
                cancellationToken).ConfigureAwait(false);

            await _definitionPublisher.PublishAsync(
                definition,
                cancellationToken).ConfigureAwait(false);

            var created = await CreateExecutionAsync(
                request,
                cancellationToken).ConfigureAwait(false);

            handle.MarkRunning(
                created.ExecutionId);

            _logger.Engine.LogInformation(
                $"[AI PIPELINE CONTROLLER] Execution created. RunId='{handle.RunId}', ExecutionId='{created.ExecutionId}', Pipeline='{created.PipelineName}'.");

            var final = await RunCreatedExecutionAsync(
                created.ExecutionId,
                cancellationToken).ConfigureAwait(false);

            if (final.Status == AiExecutionStatus.Completed)
            {
                handle.MarkCompleted();
            }
            else
            {
                handle.MarkFailed();
            }

            await InvokeRunFinalizedAsync(
                queuedRun,
                final,
                cancellationToken).ConfigureAwait(false);

            queuedRun.CompletionSource.TrySetResult(final);

            _logger.Engine.LogInformation(
                $"[AI PIPELINE CONTROLLER] Run terminal. RunId='{handle.RunId}', ExecutionId='{created.ExecutionId}', Status='{final.Status}'.");
        }

        /// <summary>
        /// Invokes the optional run lifecycle hook after a queued run has reached
        /// its terminal runtime result.
        /// </summary>
        /// <param name="queuedRun">The queued pipeline run.</param>
        /// <param name="final">The final execution record.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task InvokeRunFinalizedAsync(
            AiRuntimeQueuedPipelineRun queuedRun,
            AiExecutionRecord final,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(queuedRun);
            ArgumentNullException.ThrowIfNull(final);

            if (string.IsNullOrWhiteSpace(final.ExecutionId))
            {
                return;
            }

            await _runLifecycleHook.OnFinalizedAsync(
                new AiRuntimePipelineRunFinalizedContext
                {
                    RunId = queuedRun.Handle.RunId,
                    ExecutionId = final.ExecutionId,
                    Record = final
                },
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Advances the created runtime execution using either the default single
        /// runtime instance worker or the distributed runtime instance worker group.
        /// </summary>
        /// <param name="executionId">The runtime execution identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The terminal execution record.</returns>
        private async Task<AiExecutionRecord> RunCreatedExecutionAsync(
            string executionId,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);

            if (!_options.Distributed.Enabled)
            {
                return await _worker.RunExecutionAsync(
                    executionId,
                    cancellationToken).ConfigureAwait(false);
            }

            var workers = _workerFactory.CreateWorkers(
                _options.Distributed.WorkerCount);

            return await _workerGroup.RunExecutionAsync(
                executionId,
                workers,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a runtime execution for the specified pipeline run request.
        /// </summary>
        /// <param name="request">The pipeline run request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The created execution record.</returns>
        private async Task<AiExecutionRecord> CreateExecutionAsync(
            AiRuntimePipelineRunRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.PipelineName);

            if (request.Input is null)
            {
                return await _engine.CreateAsync(
                    request.PipelineName,
                    new Dictionary<string, object?>(),
                    cancellationToken).ConfigureAwait(false);
            }

            if (request.Input is string textInput)
            {
                return await _engine.CreateAsync(
                    request.PipelineName,
                    textInput,
                    cancellationToken).ConfigureAwait(false);
            }

            if (request.Input is IDictionary<string, object?> stateInput)
            {
                return await _engine.CreateAsync(
                    request.PipelineName,
                    stateInput,
                    cancellationToken).ConfigureAwait(false);
            }

            if (request.Input is IReadOnlyDictionary<string, object?> readonlyStateInput)
            {
                return await _engine.CreateAsync(
                    request.PipelineName,
                    new Dictionary<string, object?>(readonlyStateInput, StringComparer.Ordinal),
                    cancellationToken).ConfigureAwait(false);
            }

            return await _engine.CreateAsync(
                request.PipelineName,
                ConvertObjectToStateInput(request.Input),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Converts an arbitrary object into structured execution state input.
        /// </summary>
        /// <param name="input">The input object.</param>
        /// <returns>The structured state input dictionary.</returns>
        private static Dictionary<string, object?> ConvertObjectToStateInput(
            object input)
        {
            ArgumentNullException.ThrowIfNull(input);

            var properties = input
                .GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0)
                .ToArray();

            if (properties.Length == 0)
            {
                return new Dictionary<string, object?>
                {
                    [AiExecutionKeys.Input] = input
                };
            }

            var state = new Dictionary<string, object?>(
                StringComparer.Ordinal);

            foreach (var property in properties)
            {
                state[property.Name] = property.GetValue(input);
            }

            return state;
        }

        /// <summary>
        /// Marks all queued, not-yet-started runs as cancelled.
        /// </summary>
        private void CancelQueuedRuns()
        {
            while (_queue.Reader.TryRead(out var queuedRun))
            {
                queuedRun.Handle.MarkCancelled();

                queuedRun.CompletionSource.TrySetCanceled();

                _logger.Engine.LogInformation(
                    $"[AI PIPELINE CONTROLLER] Queued run cancelled before execution. RunId='{queuedRun.Handle.RunId}', Pipeline='{queuedRun.Request.PipelineName}'.");
            }
        }

        /// <summary>
        /// Waits while the controller queue is paused before allowing a queued run to start.
        /// </summary>
        /// <param name="queuedRun">
        /// The queued pipeline run waiting to start.
        /// </param>
        /// <param name="cancellationToken">
        /// The controller cancellation token.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous wait operation.
        /// </returns>
        /// <remarks>
        /// The queued run has already been read from the channel, but it has not acquired a
        /// parallelism slot and has not started execution creation. Its public handle therefore
        /// remains queued until the controller queue resumes.
        /// </remarks>
        private async Task WaitWhileQueuePausedAsync(
            AiRuntimeQueuedPipelineRun queuedRun,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(queuedRun);

            var logged = false;

            while (_queuePaused)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!logged)
                {
                    _logger.Engine.LogInformation(
                        $"[AI PIPELINE CONTROLLER] Queued run is waiting because the controller queue is paused. RunId='{queuedRun.Handle.RunId}', Pipeline='{queuedRun.Request.PipelineName}', Reason='{_queuePauseReason}', RequestedBy='{_queuePauseRequestedBy}', PausedAtUtc='{_queuePausedAtUtc:O}'.");

                    logged = true;
                }

                await Task.Delay(
                        TimeSpan.FromMilliseconds(25),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
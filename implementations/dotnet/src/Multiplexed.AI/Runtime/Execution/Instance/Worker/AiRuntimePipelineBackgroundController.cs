using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Control;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.Abstractions.AI.Observability.Context;
using Multiplexed.Abstractions.AI.Observability.Ledger;
using Multiplexed.Abstractions.AI.Observability.Tracing;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using Multiplexed.AI.Runtime.Execution.Engine.Core;
using Multiplexed.AI.Runtime.Observability.Helpers;
using Multiplexed.AI.Runtime.Observability.Logging;
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
    /// <para>
    /// Queue-level control is intentionally separate from execution-level control.
    /// Pausing the queue prevents new queued runs from starting but does not pause
    /// already running executions.
    /// </para>
    /// </remarks>
    public sealed class AiRuntimePipelineBackgroundController : IAiRuntimePipelineBackgroundController
    {
        private const string PipelineBackgroundControllerWorkerId =
            "pipeline-background-controller";

        private readonly AiDagExecutionEngine _engine;
        private readonly IAiRuntimeInstanceWorker _worker;
        private readonly IAiRuntimeInstanceWorkerGroup _workerGroup;
        private readonly IAiRuntimeInstanceWorkerFactory _workerFactory;
        private readonly IAiRuntimePipelineRunDefinitionResolver _definitionResolver;
        private readonly IAiRuntimePipelineRunDefinitionPublisher _definitionPublisher;
        private readonly IAiRuntimePipelineRunLifecycleHook _runLifecycleHook;
        private readonly IAiRuntimeLogger _logger;
        private readonly IAiRuntimeObservability _observability;
        private readonly IAiExecutionControlService _executionControlService;
        private readonly IAiRuntimeInstanceIdentity _runtimeInstanceIdentity;

        private readonly AiRuntimePipelineBackgroundControllerOptions _options;
        private readonly Channel<AiRuntimeQueuedPipelineRun> _queue;
        private readonly SemaphoreSlim _parallelismGate;
        private readonly ConcurrentDictionary<string, Task> _activeRuns = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, AiRuntimeQueuedPipelineRun> _queuedRuns = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, AiRuntimeQueuedPipelineRun> _runningRuns = new(StringComparer.Ordinal);
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
        /// <param name="executionControlService">The execution control service.</param>
        /// <param name="runtimeInstanceIdentity">The runtime instance identity of the controller host.</param>
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
            IAiExecutionControlService executionControlService,
            IAiRuntimeInstanceIdentity runtimeInstanceIdentity,
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
            _executionControlService = executionControlService ?? throw new ArgumentNullException(nameof(executionControlService));
            _runtimeInstanceIdentity = runtimeInstanceIdentity ?? throw new ArgumentNullException(nameof(runtimeInstanceIdentity));
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

            var correlation = new AiRuntimeExecutionCorrelationContext
            {
                CorrelationId = runId,
                RunId = runId,
                PipelineName = request.PipelineName,
                RuntimeInstanceId = _runtimeInstanceIdentity.RuntimeInstanceId,
                WorkerId = PipelineBackgroundControllerWorkerId
            };

            var completionSource = new TaskCompletionSource<AiExecutionRecord>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var handle = new AiRuntimeWorkerRunHandle(
                runId,
                completionSource.Task);

            var queuedRun = new AiRuntimeQueuedPipelineRun(
                request,
                handle,
                completionSource,
                correlation);

            _queuedRuns[runId] = queuedRun;

            try
            {
                await _queue.Writer.WriteAsync(
                    queuedRun,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _queuedRuns.TryRemove(
                    runId,
                    out _);

                throw;
            }

            _logger.Engine.LogInformation(
                $"[AI PIPELINE CONTROLLER] Run queued. RunId='{runId}', Pipeline='{request.PipelineName}'.");

            await RecordRunLedgerAsync(
                    runId,
                    request.PipelineName,
                    AiDecisionLedgerEvents.Run.Queued,
                    AiDecisionLedgerOutcome.Persisted,
                    reason: "Pipeline run queued.",
                    metadata: new Dictionary<string, string>
                    {
                        ["run.id"] = runId,
                        ["pipeline.name"] = request.PipelineName
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return handle;
        }

        /// <inheritdoc />
        public async Task PauseQueueAsync(
            AiRuntimeWorkerRunHandle handle,
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

            await RecordQueueLedgerAsync(
                    executionId: handle.ExecutionId ?? handle.RunId,
                    runId: handle.RunId,
                    pipelineName: "pipeline-controller",
                    eventType: AiDecisionLedgerEvents.Queue.Paused,
                    outcome: AiDecisionLedgerOutcome.Applied,
                    reason: reason ?? "Pipeline controller queue paused.",
                    metadata: new Dictionary<string, string>
                    {
                        ["requested.by"] = requestedBy ?? string.Empty,
                        ["paused.at.utc"] = _queuePausedAtUtc?.ToString("O") ?? string.Empty
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ResumeQueueAsync(
            AiRuntimeWorkerRunHandle handle,
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

            await RecordQueueLedgerAsync(
                    executionId: handle.ExecutionId ?? handle.RunId,
                    runId: handle.RunId,
                    pipelineName: "pipeline-controller",
                    eventType: AiDecisionLedgerEvents.Queue.Resumed,
                    outcome: AiDecisionLedgerOutcome.Applied,
                    reason: "Pipeline controller queue resumed.",
                    metadata: new Dictionary<string, string>
                    {
                        ["requested.by"] = requestedBy ?? string.Empty,
                        ["previous.requested.by"] = requestedBy ?? string.Empty,
                        ["paused.since.utc"] = pausedSince?.ToString("O") ?? string.Empty
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> CancelQueuedRunAsync(
            AiRuntimeWorkerRunHandle handle,
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(handle.RunId);
            cancellationToken.ThrowIfCancellationRequested();

            if (!_queuedRuns.TryRemove(handle.RunId, out var queuedRun))
            {
                return false;
            }

            if (queuedRun.Handle.Status != AiRuntimeWorkerRunStatus.Queued)
            {
                return false;
            }

            queuedRun.Handle.MarkCancelled();

            queuedRun.CompletionSource.TrySetResult(
                new AiExecutionRecord
                {
                    ExecutionId = handle.ExecutionId ?? handle.RunId,
                    Status = AiExecutionStatus.Cancelled,
                    CompletedAtUtc = DateTime.UtcNow
                });

            _logger.Engine.LogInformation(
                $"[AI PIPELINE CONTROLLER] Queued run cancelled. RunId='{handle.RunId}', Pipeline='{queuedRun.Request.PipelineName}', RequestedBy='{requestedBy}', Reason='{reason}'.");

            await RecordRunLedgerAsync(
                    handle.RunId,
                    queuedRun.Request.PipelineName,
                    AiDecisionLedgerEvents.Run.Cancelled,
                    AiDecisionLedgerOutcome.Cancelled,
                    reason: reason ?? "Queued pipeline run cancelled before execution creation.",
                    metadata: new Dictionary<string, string>
                    {
                        ["run.id"] = handle.RunId,
                        ["pipeline.name"] = queuedRun.Request.PipelineName,
                        ["requested.by"] = requestedBy ?? string.Empty,
                        ["run.status"] = AiRuntimeWorkerRunStatus.Cancelled.ToString()
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> CancelRunAsync(
            AiRuntimeWorkerRunHandle handle,
            string? reason = null,
            string? requestedBy = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(handle.RunId);
            cancellationToken.ThrowIfCancellationRequested();

            var queuedCancelled = await CancelQueuedRunAsync(
                    handle,
                    reason,
                    requestedBy,
                    cancellationToken)
                .ConfigureAwait(false);

            if (queuedCancelled)
            {
                return true;
            }

            if (!_runningRuns.TryGetValue(handle.RunId, out var runningRun))
            {
                return false;
            }

            var executionId = runningRun.Handle.ExecutionId;

            if (string.IsNullOrWhiteSpace(executionId))
            {
                return false;
            }

            await _executionControlService.CancelExecutionAsync(
                    executionId,
                    reason ?? "Running pipeline run cancellation requested.",
                    requestedBy,
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.Engine.LogInformation(
                $"[AI PIPELINE CONTROLLER] Running run cancellation requested. RunId='{handle.RunId}', ExecutionId='{executionId}', Pipeline='{runningRun.Request.PipelineName}', RequestedBy='{requestedBy}', Reason='{reason}'.");

            return true;
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
                    if (queuedRun.Handle.Status == AiRuntimeWorkerRunStatus.Cancelled)
                    {
                        _queuedRuns.TryRemove(
                            queuedRun.Handle.RunId,
                            out _);

                        continue;
                    }

                    await WaitWhileQueuePausedAsync(
                            queuedRun,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (queuedRun.Handle.Status == AiRuntimeWorkerRunStatus.Cancelled)
                    {
                        _queuedRuns.TryRemove(
                            queuedRun.Handle.RunId,
                            out _);

                        continue;
                    }

                    await _parallelismGate.WaitAsync(cancellationToken).ConfigureAwait(false);

                    _queuedRuns.TryRemove(
                        queuedRun.Handle.RunId,
                        out _);

                    _runningRuns[queuedRun.Handle.RunId] = queuedRun;

                    await RecordRunLedgerAsync(
                            queuedRun.Handle.RunId,
                            queuedRun.Request.PipelineName,
                            AiDecisionLedgerEvents.Run.Dequeued,
                            AiDecisionLedgerOutcome.Started,
                            reason: "Pipeline run dequeued for processing.",
                            metadata: new Dictionary<string, string>
                            {
                                ["run.id"] = queuedRun.Handle.RunId,
                                ["pipeline.name"] = queuedRun.Request.PipelineName,
                                ["max.concurrent.runs"] = _options.MaxConcurrentRuns.ToString()
                            },
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

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

                            _runningRuns.TryRemove(
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

            queuedRun.Correlation.RuntimeInstanceId ??= _runtimeInstanceIdentity.RuntimeInstanceId;
            queuedRun.Correlation.WorkerId ??= PipelineBackgroundControllerWorkerId;
            queuedRun.Correlation.RunId ??= handle.RunId;

            using var correlationScope = _observability.Correlation.Push(queuedRun.Correlation);

            try
            {
                await _observability.Tracer.TraceExecutionAsync(
                    new AiExecutionTraceContext
                    {
                        ExecutionId = handle.RunId,
                        ExecutionMode = "Dag",
                        Status = "PipelineRunQueued",
                        WorkerId = queuedRun.Correlation.WorkerId ?? PipelineBackgroundControllerWorkerId
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

                await RecordRunLedgerAsync(
                        handle.RunId,
                        request.PipelineName,
                        AiDecisionLedgerEvents.Run.Cancelled,
                        AiDecisionLedgerOutcome.Cancelled,
                        handle.ExecutionId,
                        "Pipeline run cancelled by controller cancellation token.",
                        new Dictionary<string, string>
                        {
                            ["run.id"] = handle.RunId,
                            ["pipeline.name"] = request.PipelineName,
                            ["run.status"] = AiRuntimeWorkerRunStatus.Cancelled.ToString()
                        },
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                handle.MarkFailed();

                queuedRun.CompletionSource.TrySetException(ex);

                _logger.Engine.LogError(
                    $"[AI PIPELINE CONTROLLER] Run failed. RunId='{handle.RunId}', Pipeline='{request.PipelineName}', Error='{ex.Message}'.");

                await RecordRunLedgerAsync(
                        handle.RunId,
                        request.PipelineName,
                        AiDecisionLedgerEvents.Run.Failed,
                        AiDecisionLedgerOutcome.Failed,
                        handle.ExecutionId,
                        ex.Message,
                        new Dictionary<string, string>
                        {
                            ["run.id"] = handle.RunId,
                            ["pipeline.name"] = request.PipelineName,
                            ["run.status"] = AiRuntimeWorkerRunStatus.Failed.ToString(),
                            ["exception.type"] = ex.GetType().Name
                        },
                        CancellationToken.None)
                    .ConfigureAwait(false);
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

            queuedRun.Correlation.ExecutionId = created.ExecutionId;
            queuedRun.Correlation.PipelineKey = created.PipelineName;
            queuedRun.Correlation.RunId = handle.RunId;

            handle.MarkRunning(
                created.ExecutionId);

            await RecordRunLedgerAsync(
                    handle.RunId,
                    request.PipelineName,
                    AiDecisionLedgerEvents.Run.Started,
                    AiDecisionLedgerOutcome.Started,
                    created.ExecutionId,
                    "Pipeline run started execution processing.",
                    new Dictionary<string, string>
                    {
                        ["run.id"] = handle.RunId,
                        ["execution.id"] = created.ExecutionId,
                        ["pipeline.name"] = request.PipelineName,
                        ["distributed.enabled"] = _options.Distributed.Enabled.ToString(),
                        ["distributed.worker.count"] = _options.Distributed.WorkerCount.ToString()
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.Engine.LogInformation(
                $"[AI PIPELINE CONTROLLER] Execution created. RunId='{handle.RunId}', ExecutionId='{created.ExecutionId}', Pipeline='{created.PipelineName}'.");

            var final = await RunCreatedExecutionAsync(
                created.ExecutionId,
                cancellationToken).ConfigureAwait(false);

            if (final.Status == AiExecutionStatus.Completed)
            {
                handle.MarkCompleted();
            }
            else if (final.Status == AiExecutionStatus.Cancelled)
            {
                handle.MarkCancelled();
            }
            else
            {
                handle.MarkFailed();
            }

            await RecordRunTerminalLedgerAsync(
                    handle.RunId,
                    request.PipelineName,
                    created.ExecutionId,
                    final,
                    cancellationToken)
                .ConfigureAwait(false);

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
        /// Records a controller run lifecycle event in the decision ledger.
        /// </summary>
        /// <param name="runId">The controller run identifier.</param>
        /// <param name="pipelineName">The pipeline name.</param>
        /// <param name="eventType">The ledger event type.</param>
        /// <param name="outcome">The ledger outcome.</param>
        /// <param name="executionId">The optional runtime execution identifier.</param>
        /// <param name="reason">The optional event reason.</param>
        /// <param name="metadata">The optional metadata.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task RecordRunLedgerAsync(
            string runId,
            string pipelineName,
            string eventType,
            AiDecisionLedgerOutcome outcome,
            string? executionId = null,
            string? reason = null,
            IReadOnlyDictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(runId);
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
            ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

            var resolvedExecutionId = string.IsNullOrWhiteSpace(executionId)
                ? runId
                : executionId;

            var context = AiRuntimeCorrelationContextHelper.Create(
                executionId: resolvedExecutionId,
                pipelineKey: pipelineName,
                stepName: "pipeline-run",
                workerId: PipelineBackgroundControllerWorkerId,
                claimToken: null,
                concurrencyContext: null,
                runId: runId,
                correlationId: runId);

            context.CorrelationId = _observability.Correlation.Current?.CorrelationId ?? runId;

            await _observability.Ledger.RecordAsync(
                    context,
                    AiDecisionLedgerCategory.Run,
                    eventType,
                    outcome,
                    reason,
                    metadata,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Records a controller queue lifecycle event in the decision ledger.
        /// </summary>
        /// <param name="eventType">The ledger event type.</param>
        /// <param name="outcome">The ledger outcome.</param>
        /// <param name="reason">The optional event reason.</param>
        /// <param name="metadata">The optional metadata.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task RecordQueueLedgerAsync(
            string executionId,
            string runId,
            string pipelineName,
            string eventType,
            AiDecisionLedgerOutcome outcome,
            string? reason = null,
            IReadOnlyDictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(runId);
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
            ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

            var context = AiRuntimeCorrelationContextHelper.Create(
                executionId: executionId,
                pipelineKey: pipelineName,
                stepName: "pipeline-queue",
                workerId: PipelineBackgroundControllerWorkerId,
                claimToken: null,
                concurrencyContext: null,
                runId: runId,
                correlationId: runId);

            context.CorrelationId = _observability.Correlation.Current?.CorrelationId ?? runId;

            await _observability.Ledger.RecordAsync(
                    context,
                    AiDecisionLedgerCategory.Queue,
                    eventType,
                    outcome,
                    reason,
                    metadata,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Records the terminal controller run lifecycle event in the decision ledger.
        /// </summary>
        /// <param name="runId">The controller run identifier.</param>
        /// <param name="pipelineName">The pipeline name.</param>
        /// <param name="executionId">The runtime execution identifier.</param>
        /// <param name="final">The final execution record.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task RecordRunTerminalLedgerAsync(
            string runId,
            string pipelineName,
            string executionId,
            AiExecutionRecord final,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(runId);
            ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
            ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
            ArgumentNullException.ThrowIfNull(final);

            var eventType = final.Status == AiExecutionStatus.Completed
                ? AiDecisionLedgerEvents.Run.Completed
                : final.Status == AiExecutionStatus.Cancelled
                    ? AiDecisionLedgerEvents.Run.Cancelled
                    : AiDecisionLedgerEvents.Run.Failed;

            var outcome = final.Status == AiExecutionStatus.Completed
                ? AiDecisionLedgerOutcome.Completed
                : final.Status == AiExecutionStatus.Cancelled
                    ? AiDecisionLedgerOutcome.Cancelled
                    : AiDecisionLedgerOutcome.Failed;

            await RecordRunLedgerAsync(
                    runId,
                    pipelineName,
                    eventType,
                    outcome,
                    executionId,
                    $"Pipeline run reached terminal status '{final.Status}'.",
                    new Dictionary<string, string>
                    {
                        ["run.id"] = runId,
                        ["execution.id"] = executionId,
                        ["pipeline.name"] = pipelineName,
                        ["execution.status"] = final.Status.ToString(),
                        ["completed.at.utc"] = final.CompletedAtUtc.ToString("O") ?? string.Empty
                    },
                    cancellationToken)
                .ConfigureAwait(false);
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
            foreach (var item in _queuedRuns.ToArray())
            {
                if (!_queuedRuns.TryRemove(item.Key, out var queuedRun))
                {
                    continue;
                }

                queuedRun.Handle.MarkCancelled();

                queuedRun.CompletionSource.TrySetCanceled();

                _logger.Engine.LogInformation(
                    $"[AI PIPELINE CONTROLLER] Queued run cancelled before execution. RunId='{queuedRun.Handle.RunId}', Pipeline='{queuedRun.Request.PipelineName}'.");
            }

            while (_queue.Reader.TryRead(out var queuedRun))
            {
                if (queuedRun.Handle.Status == AiRuntimeWorkerRunStatus.Cancelled)
                {
                    continue;
                }

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
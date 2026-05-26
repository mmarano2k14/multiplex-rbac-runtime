using Multiplexed.Abstractions.AI.Execution.Control;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Control
{
    /// <summary>
    /// Executes pause, resume, and cancel commands against the durable execution control service
    /// and the runtime background controller.
    /// </summary>
    public sealed class EnterpriseRuntimeExecutionControlCommandExecutor
    {
        private const string RequestedBy = "enterprise-runtime-console";

        private readonly IAiExecutionControlService _controlService;
        private readonly IAiRuntimePipelineBackgroundController _controller;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnterpriseRuntimeExecutionControlCommandExecutor"/> class.
        /// </summary>
        /// <param name="controlService">
        /// The durable execution control service.
        /// </param>
        /// <param name="controller">
        /// The runtime background controller.
        /// </param>
        public EnterpriseRuntimeExecutionControlCommandExecutor(
            IAiExecutionControlService controlService,
            IAiRuntimePipelineBackgroundController controller)
        {
            _controlService = controlService ?? throw new ArgumentNullException(
                nameof(controlService));

            _controller = controller ?? throw new ArgumentNullException(
                nameof(controller));
        }

        /// <summary>
        /// Requests execution pause.
        /// </summary>
        /// <param name="handle">
        /// The runtime run handle.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        public Task PauseAsync(
            AiRuntimeWorkerRunHandle handle,
            CancellationToken cancellationToken)
        {
            var executionId = ResolveExecutionId(
                handle);

            return _controlService.PauseExecutionAsync(
                executionId,
                reason: "Console pause requested.",
                requestedBy: RequestedBy,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Requests execution resume.
        /// </summary>
        /// <param name="handle">
        /// The runtime run handle.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        public Task ResumeAsync(
            AiRuntimeWorkerRunHandle handle,
            CancellationToken cancellationToken)
        {
            var executionId = ResolveExecutionId(
                handle);

            return _controlService.ResumeExecutionAsync(
                executionId,
                requestedBy: RequestedBy,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Requests execution cancellation and cancels the controller run handle.
        /// </summary>
        /// <param name="handle">
        /// The runtime run handle.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        public async Task CancelAsync(
            AiRuntimeWorkerRunHandle handle,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(
                handle);

            var executionId = ResolveExecutionId(
                handle);

            await _controlService.CancelExecutionAsync(
                    executionId,
                    reason: "Console cancellation confirmed.",
                    requestedBy: RequestedBy,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await _controller.CancelRunAsync(
                    handle,
                    reason: "Console cancellation confirmed.",
                    requestedBy: RequestedBy,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves the execution identifier from the run handle.
        /// </summary>
        /// <param name="handle">
        /// The runtime run handle.
        /// </param>
        /// <returns>
        /// The execution identifier.
        /// </returns>
        private static string ResolveExecutionId(
            AiRuntimeWorkerRunHandle handle)
        {
            ArgumentNullException.ThrowIfNull(
                handle);

            if (string.IsNullOrWhiteSpace(
                    handle.ExecutionId))
            {
                throw new InvalidOperationException(
                    "Execution control is not available before the execution id is created.");
            }

            return handle.ExecutionId;
        }
    }
}
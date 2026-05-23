using Multiplexed.Abstractions.AI.Execution.Instance.Worker;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Control
{
    /// <summary>
    /// Listens for interactive execution control hotkeys.
    /// </summary>
    public sealed class EnterpriseRuntimeExecutionControlHotkeyListener
    {
        private readonly EnterpriseRuntimeExecutionControlCommandExecutor _commandExecutor;
        private readonly EnterpriseRuntimeExecutionControlState _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnterpriseRuntimeExecutionControlHotkeyListener"/> class.
        /// </summary>
        /// <param name="commandExecutor">
        /// The execution control command executor.
        /// </param>
        /// <param name="state">
        /// The shared execution control state.
        /// </param>
        public EnterpriseRuntimeExecutionControlHotkeyListener(
            EnterpriseRuntimeExecutionControlCommandExecutor commandExecutor,
            EnterpriseRuntimeExecutionControlState state)
        {
            _commandExecutor = commandExecutor ?? throw new ArgumentNullException(
                nameof(commandExecutor));

            _state = state ?? throw new ArgumentNullException(
                nameof(state));
        }

        /// <summary>
        /// Listens for execution control hotkeys until cancellation is requested.
        /// </summary>
        /// <param name="handle">
        /// The runtime run handle.
        /// </param>
        /// <param name="runnerCancellationSource">
        /// The local runner cancellation source used to unblock the console runner after cancel.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        public async Task ListenAsync(
            AiRuntimeWorkerRunHandle handle,
            CancellationTokenSource runnerCancellationSource,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(
                handle);

            ArgumentNullException.ThrowIfNull(
                runnerCancellationSource);

            _state.RunnerCancellationSource = runnerCancellationSource;

            PrintControls();

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!Console.KeyAvailable)
                {
                    await Task.Delay(
                            TimeSpan.FromMilliseconds(100),
                            cancellationToken)
                        .ConfigureAwait(false);

                    continue;
                }

                var key = Console.ReadKey(
                    intercept: true);

                if (key.Key == ConsoleKey.Spacebar)
                {
                    await TogglePauseResumeAsync(
                            handle,
                            cancellationToken)
                        .ConfigureAwait(false);

                    continue;
                }

                if (key.Key == ConsoleKey.C &&
                    key.Modifiers.HasFlag(
                        ConsoleModifiers.Shift))
                {
                    await ConfirmCancelAsync(
                            handle,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return;
                }
            }
        }

        /// <summary>
        /// Toggles between pause and resume.
        /// </summary>
        /// <param name="handle">
        /// The runtime run handle.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        private async Task TogglePauseResumeAsync(
            AiRuntimeWorkerRunHandle handle,
            CancellationToken cancellationToken)
        {
            if (_state.IsPaused)
            {
                await _commandExecutor.ResumeAsync(
                        handle,
                        cancellationToken)
                    .ConfigureAwait(false);

                _state.IsPaused = false;
                _state.RealtimeOutputSuspended = false;

                WriteControlLine(
                    "[CONTROL] resumed");

                return;
            }

            await _commandExecutor.PauseAsync(
                    handle,
                    cancellationToken)
                .ConfigureAwait(false);

            _state.IsPaused = true;
            _state.RealtimeOutputSuspended = true;

            WriteControlLine(
                "[CONTROL] paused");
        }

        /// <summary>
        /// Pauses execution and asks the user to confirm cancellation.
        /// </summary>
        /// <param name="handle">
        /// The runtime run handle.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        private async Task ConfirmCancelAsync(
            AiRuntimeWorkerRunHandle handle,
            CancellationToken cancellationToken)
        {
            if (!_state.IsPaused)
            {
                await _commandExecutor.PauseAsync(
                        handle,
                        cancellationToken)
                    .ConfigureAwait(false);

                _state.IsPaused = true;
                _state.RealtimeOutputSuspended = true;

                WriteControlLine(
                    "[CONTROL] paused before cancel confirmation");
            }

            var confirmed = EnterpriseRuntimeExecutionCancelConfirmationMenu.Confirm();

            if (confirmed)
            {
                _state.IsCancelRequested = true;
                _state.RealtimeOutputSuspended = true;

                await _commandExecutor.CancelAsync(
                        handle,
                        cancellationToken)
                    .ConfigureAwait(false);

                _state.RunnerCancellationSource?.Cancel();

                WriteControlLine(
                    "[CONTROL] cancel confirmed");

                return;
            }

            await _commandExecutor.ResumeAsync(
                    handle,
                    cancellationToken)
                .ConfigureAwait(false);

            _state.IsPaused = false;
            _state.RealtimeOutputSuspended = false;

            WriteControlLine(
                "[CONTROL] cancel declined, resumed");
        }

        /// <summary>
        /// Prints execution control shortcuts.
        /// </summary>
        private static void PrintControls()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Controls:");
            Console.WriteLine("  Space    Pause / Resume execution");
            Console.WriteLine("  Shift+C  Cancel with confirmation");
            Console.ResetColor();
            Console.WriteLine();
        }

        /// <summary>
        /// Writes a control status line.
        /// </summary>
        /// <param name="message">
        /// The control message.
        /// </param>
        private static void WriteControlLine(
            string message)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(
                message);
            Console.ResetColor();
        }
    }
}
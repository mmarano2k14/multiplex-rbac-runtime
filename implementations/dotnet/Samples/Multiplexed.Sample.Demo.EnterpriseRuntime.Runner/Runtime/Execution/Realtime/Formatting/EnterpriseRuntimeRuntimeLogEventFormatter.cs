namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Realtime.Formatting
{
    /// <summary>
    /// Formats readable enterprise runtime realtime events for console output.
    /// </summary>
    public sealed class EnterpriseRuntimeRuntimeLogEventFormatter
    {
        /// <summary>
        /// Formats a readable realtime event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        public string Format(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            ArgumentNullException.ThrowIfNull(
                readableEvent);

            return readableEvent.Kind switch
            {
                EnterpriseRuntimeRealtimeEventKind.StepClaimed =>
                    FormatStepClaimed(
                        readableEvent),

                EnterpriseRuntimeRealtimeEventKind.StepCompleted =>
                    FormatStepCompleted(
                        readableEvent),

                EnterpriseRuntimeRealtimeEventKind.StepFailed =>
                    FormatStepFailed(
                        readableEvent),

                EnterpriseRuntimeRealtimeEventKind.StepThrottled =>
                    FormatStepThrottled(
                        readableEvent),

                EnterpriseRuntimeRealtimeEventKind.FinalizationSucceeded =>
                    FormatFinalizationSucceeded(
                        readableEvent),

                EnterpriseRuntimeRealtimeEventKind.FinalizationRaceLost =>
                    FormatFinalizationRaceLost(
                        readableEvent),

                EnterpriseRuntimeRealtimeEventKind.SnapshotPersisted =>
                    FormatSnapshotPersisted(
                        readableEvent),

                EnterpriseRuntimeRealtimeEventKind.CleanupSkipped =>
                    FormatCleanupSkipped(
                        readableEvent),

                EnterpriseRuntimeRealtimeEventKind.ReplayRestored =>
                    FormatReplayRestored(
                        readableEvent),

                EnterpriseRuntimeRealtimeEventKind.RetryOrRecovery =>
                    FormatRetryOrRecovery(
                        readableEvent),

                _ => FormatDiagnostic(
                    readableEvent)
            };
        }

        /// <summary>
        /// Formats a step claimed event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        private static string FormatStepClaimed(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            return $"[CLAIMED] {ValueOrUnknown(readableEvent.StepName)} | worker={Shorten(readableEvent.WorkerId)} | token={Shorten(readableEvent.ClaimToken)}";
        }

        /// <summary>
        /// Formats a step completed event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        private static string FormatStepCompleted(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            return $"[DONE]    {ValueOrUnknown(readableEvent.StepName)} | source={Trim(readableEvent.SourceSignature, 160)}";
        }

        /// <summary>
        /// Formats a step failed event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        private static string FormatStepFailed(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            var error = string.IsNullOrWhiteSpace(
                readableEvent.Error)
                ? readableEvent.Message
                : readableEvent.Error;

            return $"[FAILED]  {ValueOrUnknown(readableEvent.StepName)} | {Trim(error, 140)}";
        }

        /// <summary>
        /// Formats a throttled step event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        private static string FormatStepThrottled(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            return $"[THROTTLED] {ValueOrUnknown(readableEvent.StepName)} | {Trim(readableEvent.Message, 160)}";
        }

        /// <summary>
        /// Formats a retry or recovery event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        private static string FormatRetryOrRecovery(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            return $"[RETRY]   {ValueOrUnknown(readableEvent.StepName)} | {Trim(readableEvent.Message, 140)}";
        }

        /// <summary>
        /// Formats a finalization succeeded event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        private static string FormatFinalizationSucceeded(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            return $"[FINAL]   succeeded | status={ValueOrUnknown(readableEvent.Status)}";
        }

        /// <summary>
        /// Formats a finalization race lost event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        private static string FormatFinalizationRaceLost(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            return "[FINAL]   skipped | already finalized by another worker";
        }

        /// <summary>
        /// Formats a snapshot persisted event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        private static string FormatSnapshotPersisted(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            return $"[SNAPSHOT] persisted | execution={Shorten(readableEvent.ExecutionId)} | status={ValueOrUnknown(readableEvent.Status)}";
        }

        /// <summary>
        /// Formats a cleanup skipped event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        private static string FormatCleanupSkipped(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            return $"[CLEANUP] skipped | execution={Shorten(readableEvent.ExecutionId)}";
        }

        /// <summary>
        /// Formats a replay restored event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        private static string FormatReplayRestored(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            return $"[REPLAY]  restored | execution={Shorten(readableEvent.ExecutionId)} | status={ValueOrUnknown(readableEvent.Status)}";
        }

        /// <summary>
        /// Formats a diagnostic event.
        /// </summary>
        /// <param name="readableEvent">
        /// The readable realtime event.
        /// </param>
        /// <returns>
        /// The formatted console line.
        /// </returns>
        private static string FormatDiagnostic(
            EnterpriseRuntimeReadableRealtimeEvent readableEvent)
        {
            return $"[EVENT]   {readableEvent.Category} | {Trim(readableEvent.Message, 160)}";
        }

        /// <summary>
        /// Shortens a long value for console output.
        /// </summary>
        /// <param name="value">
        /// The value to shorten.
        /// </param>
        /// <returns>
        /// The shortened value.
        /// </returns>
        private static string Shorten(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return "?";
            }

            if (value.Length <= 16)
            {
                return value;
            }

            return value[..16] + "...";
        }

        /// <summary>
        /// Returns a fallback value when text is missing.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <returns>
        /// The value or a fallback.
        /// </returns>
        private static string ValueOrUnknown(
            string? value)
        {
            return string.IsNullOrWhiteSpace(
                value)
                ? "?"
                : value;
        }

        /// <summary>
        /// Trims a value to the configured maximum length.
        /// </summary>
        /// <param name="value">
        /// The value to trim.
        /// </param>
        /// <param name="maxLength">
        /// The maximum length.
        /// </param>
        /// <returns>
        /// The trimmed value.
        /// </returns>
        private static string Trim(
            string? value,
            int maxLength)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength
                ? value
                : value[..maxLength] + "...";
        }
    }
}
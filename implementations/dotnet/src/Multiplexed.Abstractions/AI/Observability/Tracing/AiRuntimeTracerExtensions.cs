using System;
using System.Threading.Tasks;

namespace Multiplexed.Abstractions.AI.Observability.Tracing
{
    /// <summary>
    /// Extension methods for <see cref="IAiRuntimeTracer"/>.
    /// </summary>
    public static class AiRuntimeTracerExtensions
    {
        /// <summary>
        /// Executes an asynchronous operation inside an execution trace scope.
        /// </summary>
        public static Task<T> TraceExecutionAsync<T>(
            this IAiRuntimeTracer tracer,
            AiExecutionTraceContext context,
            Func<Task<T>> action)
        {
            return TraceAsync(tracer, () => tracer.StartExecution(context), action);
        }

        /// <summary>
        /// Executes an asynchronous operation inside a step trace scope.
        /// </summary>
        public static Task<T> TraceStepAsync<T>(
            this IAiRuntimeTracer tracer,
            AiStepTraceContext context,
            Func<Task<T>> action)
        {
            return TraceAsync(tracer, () => tracer.StartStep(context), action);
        }

        /// <summary>
        /// Executes an asynchronous operation inside a retention trace scope.
        /// </summary>
        public static Task<T> TraceRetentionAsync<T>(
            this IAiRuntimeTracer tracer,
            AiRetentionTraceContext context,
            Func<Task<T>> action)
        {
            return TraceAsync(tracer, () => tracer.StartRetention(context), action);
        }

        /// <summary>
        /// Executes an asynchronous operation inside a storage trace scope.
        /// </summary>
        public static Task<T> TraceStorageAsync<T>(
            this IAiRuntimeTracer tracer,
            AiStorageTraceContext context,
            Func<Task<T>> action)
        {
            return TraceAsync(tracer, () => tracer.StartStorage(context), action);
        }

        public static async Task<T> TraceStorageAsync<T>(
            this IAiRuntimeTracer tracer,
            AiStorageTraceContext context,
            Func<IAiTraceScope, Task<T>> action)
        {
            ArgumentNullException.ThrowIfNull(tracer);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(action);

            var scope = tracer.StartStorage(context);

            try
            {
                var result = await action(scope).ConfigureAwait(false);
                scope.SetSuccess();
                return result;
            }
            catch (Exception ex)
            {
                scope.SetError(ex);
                throw;
            }
            finally
            {
                scope.Dispose();
            }
        }

        /// <summary>
        /// Executes an asynchronous operation inside a resolver trace scope.
        /// </summary>
        public static Task<T> TraceResolverAsync<T>(
            this IAiRuntimeTracer tracer,
            AiResolverTraceContext context,
            Func<Task<T>> action)
        {
            return TraceAsync(tracer, () => tracer.StartResolver(context), action);
        }

        public static async Task<T> TraceRetentionAsync<T>(
            this IAiRuntimeTracer tracer,
            AiRetentionTraceContext context,
            Func<IAiTraceScope, Task<T>> action)
        {
            ArgumentNullException.ThrowIfNull(tracer);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(action);

            var scope = tracer.StartRetention(context);

            try
            {
                var result = await action(scope).ConfigureAwait(false);
                scope.SetSuccess();
                return result;
            }
            catch (Exception ex)
            {
                scope.SetError(ex);
                throw;
            }
            finally
            {
                scope.Dispose();
            }
        }

        private static async Task<T> TraceAsync<T>(
            IAiRuntimeTracer tracer,
            Func<IAiTraceScope> startScope,
            Func<Task<T>> action)
        {
            ArgumentNullException.ThrowIfNull(tracer);
            ArgumentNullException.ThrowIfNull(startScope);
            ArgumentNullException.ThrowIfNull(action);

            var scope = startScope();

            try
            {
                var result = await action().ConfigureAwait(false);
                scope.SetSuccess();
                return result;
            }
            catch (Exception ex)
            {
                scope.SetError(ex);
                throw;
            }
            finally
            {
                scope.Dispose();
            }
        }
    }
}
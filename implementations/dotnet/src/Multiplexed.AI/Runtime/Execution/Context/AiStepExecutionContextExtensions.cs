using Microsoft.Extensions.DependencyInjection;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using System.Runtime.CompilerServices;

namespace Multiplexed.AI.Runtime.Execution.Context
{
    /// <summary>
    /// Provides cached access to the step-scoped AI context helper.
    /// </summary>
    public static class AiStepExecutionContextExtensions
    {
        private static readonly ConditionalWeakTable<AiStepExecutionContext, IAiStepContextHelper> Cache = new();

        /// <summary>
        /// Gets the cached helper for the current step context, or creates it on first access.
        ///
        /// BEHAVIOR:
        /// - One helper instance per AiStepExecutionContext.
        /// - No repeated DI lookup inside steps.
        /// - Automatically released when the step context is garbage collected.
        /// </summary>
        public static IAiStepContextHelper GetHelper(this AiStepExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return Cache.GetValue(context, static ctx =>
            {
                var factory = ctx.Services.GetService<IAiStepContextHelperFactory>();

                if (factory is not null)
                {
                    return factory.Create(ctx);
                }

                // Fallback SAFE (tests / legacy hosts)
                var resolver = ctx.Services.GetService<IAiContextValueResolver>()
                    ?? new DefaultAiContextValueResolver();

                return new DefaultAiStepContextHelper(ctx, resolver);
            });
        }
    }
}
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.Abstractions.AI.Memory;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the shared execution context passed across AI runtime components.
    ///
    /// PURPOSE:
    /// - Exposes the persisted execution record.
    /// - Exposes the mutable execution state.
    /// - Provides access to scoped services and cancellation.
    /// - Provides short-lived execution memory.
    /// - Provides controlled read/write access to execution state via reader/writer.
    ///
    /// DESIGN:
    /// - This is execution-scoped, not step-scoped.
    /// - Step-specific input/config/result resolution is handled by IAiStepContextHelper.
    /// - Path and payload resolution are handled by IAiContextValueResolver.
    ///
    /// ARCHITECTURE:
    /// - Read access → IAiExecutionStateReader
    /// - Write access → IAiExecutionStateWriter
    /// - State object → persistence model only
    ///
    /// IMPORTANT:
    /// - AiExecutionRecord is the orchestration summary.
    /// - AiExecutionState contains durable runtime data and per-step state.
    /// - AiWorkingMemory is transient and not persisted.
    /// </summary>
    public sealed class AiExecutionContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionContext"/> class.
        /// </summary>
        public AiExecutionContext(
            AiExecutionRecord record,
            AiExecutionState state,
            IServiceProvider services,
            IAiExecutionStateReader stateReader,
            IAiExecutionStateWriter stateWriter,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(stateReader);
            ArgumentNullException.ThrowIfNull(stateWriter);

            Record = record;
            State = state;
            Services = services;
            StateReader = stateReader;
            StateWriter = stateWriter;
            CancellationToken = cancellationToken;
            Memory = new AiWorkingMemory();
        }

        /// <summary>
        /// Gets the persisted orchestration record.
        /// </summary>
        public AiExecutionRecord Record { get; }

        /// <summary>
        /// Gets the mutable execution state (persistence model).
        /// </summary>
        public AiExecutionState State { get; }

        /// <summary>
        /// Gets the payload-aware execution state reader.
        /// </summary>
        public IAiExecutionStateReader StateReader { get; }

        /// <summary>
        /// Gets the execution state writer.
        /// </summary>
        public IAiExecutionStateWriter StateWriter { get; }

        /// <summary>
        /// Gets the execution-scoped working memory.
        /// </summary>
        public AiWorkingMemory Memory { get; }

        /// <summary>
        /// Gets the scoped service provider for the current execution.
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// Gets the current execution identifier.
        /// </summary>
        public string ExecutionId => Record.ExecutionId;

        /// <summary>
        /// Gets the active cancellation token for the execution scope.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        // ---------------------------------------------------------
        // READ API (payload-aware)
        // ---------------------------------------------------------

        /// <summary>
        /// Reads execution-level data using payload-aware resolution.
        /// </summary>
        public Task<T?> GetDataAsync<T>(string key)
        {
            return StateReader.GetDataAsync<T>(State, key, CancellationToken);
        }

        /// <summary>
        /// Reads execution metadata using payload-aware resolution.
        /// </summary>
        public Task<T?> GetMetadataAsync<T>(string key)
        {
            return StateReader.GetMetadataAsync<T>(State, key, CancellationToken);
        }

        // ---------------------------------------------------------
        // WRITE API
        // ---------------------------------------------------------

        /// <summary>
        /// Stores or replaces an execution-level data value.
        /// </summary>
        public void SetData<T>(string key, T value)
        {
            StateWriter.SetData(State, key, value);
        }

        /// <summary>
        /// Removes an execution-level data value.
        /// </summary>
        public bool RemoveData(string key)
        {
            return StateWriter.RemoveData(State, key);
        }

        /// <summary>
        /// Stores or replaces a payload-backed execution data value.
        /// </summary>
        public void SetDataPayload(string key, Payloads.AiStoredPayload payload)
        {
            StateWriter.SetDataPayload(State, key, payload);
        }

        /// <summary>
        /// Stores or replaces execution metadata.
        /// </summary>
        public void SetMetadata<T>(string key, T value)
        {
            StateWriter.SetMetadata(State, key, value);
        }

        /// <summary>
        /// Removes execution metadata.
        /// </summary>
        public bool RemoveMetadata(string key)
        {
            return StateWriter.RemoveMetadata(State, key);
        }

        // ---------------------------------------------------------
        // STEP API
        // ---------------------------------------------------------

        /// <summary>
        /// Ensures a step is initialized in the execution state.
        /// </summary>
        public void EnsureStepInitialized(Abstractions.AI.Pipeline.ResolvedAiPipelineStep step)
        {
            StateWriter.EnsureStepInitialized(State, step);
        }

        /// <summary>
        /// Gets or creates a step state.
        /// </summary>
        public AiStepState GetOrCreateStep(string stepName)
        {
            return StateWriter.GetOrCreateStep(State, stepName);
        }

        /// <summary>
        /// Sets the result for a step.
        /// </summary>
        public void SetStepResult(string stepName, AiStepResult result)
        {
            StateWriter.SetStepResult(State, stepName, result);
        }

        // ---------------------------------------------------------
        // SERVICES
        // ---------------------------------------------------------

        /// <summary>
        /// Resolves a required scoped service from the execution service provider.
        /// </summary>
        public T GetRequiredService<T>() where T : notnull
        {
            return (T)(Services.GetService(typeof(T))
                ?? throw new InvalidOperationException(
                    $"Required service '{typeof(T).FullName}' is not registered."));
        }
    }
}
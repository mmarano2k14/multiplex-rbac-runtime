using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.ControlPlane.Admission;
using Multiplexed.Abstractions.AI.ControlPlane.SharedController.Store;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Abstractions.AI.Runtime.Execution.Instance.Worker;
using StackExchange.Redis;
using System.Globalization;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.ControlPlane.SharedController.Store
{
    /// <summary>
    /// Redis-backed implementation of the shared runtime controller run store.
    /// </summary>
    /// <remarks>
    /// This store uses:
    /// - one Redis hash per shared run
    /// - one Redis sorted set index ordered by submission time
    /// - Lua for atomic create
    /// - Lua for atomic cancel-if-non-terminal
    /// - Lua for atomic mark-dispatched updates
    ///
    /// Redis keys:
    /// - ai:shared-runs:run:{sharedRunId}
    /// - ai:shared-runs:index
    /// </remarks>
    public sealed class RedisAiSharedRunStore : IAiSharedRunStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IDatabase _database;
        private readonly RedisAiSharedRunStoreOptions _options;
        private readonly RedisAiSharedRunStoreScriptCache _scripts;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisAiSharedRunStore"/> class.
        /// </summary>
        /// <param name="connection">The Redis connection multiplexer.</param>
        /// <param name="options">The Redis shared run store options.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="connection"/> or <paramref name="options"/> is null.
        /// </exception>
        public RedisAiSharedRunStore(
            IConnectionMultiplexer connection,
            IOptions<RedisAiSharedRunStoreOptions> options)
        {
            ArgumentNullException.ThrowIfNull(connection);

            _database = connection.GetDatabase();
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _scripts = new RedisAiSharedRunStoreScriptCache(connection);
        }

        /// <inheritdoc />
        public async Task<AiSharedRunRecord> CreateAsync(
            AiSharedRunRecord record,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentException.ThrowIfNullOrWhiteSpace(record.SharedRunId);

            cancellationToken.ThrowIfCancellationRequested();

            var runKey = _options.BuildRunKey(record.SharedRunId);
            var submittedAtScore = record.SubmittedAtUtc.ToUnixTimeMilliseconds();
            var expireSeconds = GetExpireSeconds();

            var values = BuildCreateValues(
                record,
                submittedAtScore,
                expireSeconds);

            var result = await _scripts
                .ExecuteCreateAsync(
                    _database,
                    new RedisKey[]
                    {
                        runKey,
                        _options.IndexKey
                    },
                    values)
                .ConfigureAwait(false);

            var status = result.ToString();

            if (string.Equals(status, "duplicate", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Shared run '{record.SharedRunId}' already exists.");
            }

            if (!string.Equals(status, "created", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected Redis create result for shared run '{record.SharedRunId}': '{status}'.");
            }

            return record;
        }

        /// <inheritdoc />
        public async Task<AiSharedRunRecord?> GetAsync(
            string sharedRunId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sharedRunId);

            cancellationToken.ThrowIfCancellationRequested();

            var runKey = _options.BuildRunKey(sharedRunId);

            var entries = await _database
                .HashGetAllAsync(runKey)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (entries.Length == 0)
            {
                return null;
            }

            return MapRecord(entries);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AiSharedRunRecord>> ListAsync(
            bool includeCancelled = false,
            bool includeCompleted = false,
            bool includeFailed = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ids = await _database
                .SortedSetRangeByScoreAsync(
                    _options.IndexKey,
                    order: Order.Ascending,
                    take: _options.ListScanLimit)
                .ConfigureAwait(false);

            var records = new List<AiSharedRunRecord>();

            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sharedRunId = id.ToString();

                if (string.IsNullOrWhiteSpace(sharedRunId))
                {
                    continue;
                }

                var record = await GetAsync(
                        sharedRunId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (record is null)
                {
                    continue;
                }

                if (!includeCancelled &&
                    record.Status == AiSharedRunStatus.Cancelled)
                {
                    continue;
                }

                if (!includeCompleted &&
                    record.Status == AiSharedRunStatus.Completed)
                {
                    continue;
                }

                if (!includeFailed &&
                    record.Status == AiSharedRunStatus.Failed)
                {
                    continue;
                }

                records.Add(record);
            }

            return records
                .OrderBy(record => record.SubmittedAtUtc)
                .ThenBy(record => record.SharedRunId, StringComparer.Ordinal)
                .ToArray();
        }

        /// <inheritdoc />
        public async Task<AiSharedRunRecord?> CancelAsync(
            string sharedRunId,
            string? reason = null,
            string? requestedBy = null,
            string? source = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sharedRunId);

            cancellationToken.ThrowIfCancellationRequested();

            var runKey = _options.BuildRunKey(sharedRunId);
            var updatedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var cancellationReason = string.IsNullOrWhiteSpace(reason)
                ? "Shared run cancelled."
                : reason;

            var result = await _scripts
                .ExecuteCancelAsync(
                    _database,
                    new RedisKey[]
                    {
                        runKey
                    },
                    new RedisValue[]
                    {
                        cancellationReason,
                        requestedBy ?? string.Empty,
                        source ?? string.Empty,
                        updatedAtUtc
                    })
                .ConfigureAwait(false);

            var status = result.ToString();

            if (string.Equals(status, "missing", StringComparison.Ordinal))
            {
                return null;
            }

            if (string.Equals(status, "cancelled", StringComparison.Ordinal) ||
                string.Equals(status, "terminal", StringComparison.Ordinal))
            {
                return await GetAsync(
                        sharedRunId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            throw new InvalidOperationException(
                $"Unexpected Redis cancel result for shared run '{sharedRunId}': '{status}'.");
        }

        /// <inheritdoc />
        public async Task<AiSharedRunRecord?> MarkDispatchedAsync(
            string sharedRunId,
            string runtimeInstanceId,
            string? localRunId = null,
            string? executionId = null,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sharedRunId);
            ArgumentException.ThrowIfNullOrWhiteSpace(runtimeInstanceId);

            cancellationToken.ThrowIfCancellationRequested();

            var runKey = _options.BuildRunKey(sharedRunId);
            var updatedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            var result = await _scripts
                .ExecuteMarkDispatchedAsync(
                    _database,
                    new RedisKey[]
                    {
                        runKey
                    },
                    new RedisValue[]
                    {
                        runtimeInstanceId,
                        localRunId ?? string.Empty,
                        executionId ?? string.Empty,
                        reason ?? string.Empty,
                        updatedAtUtc
                    })
                .ConfigureAwait(false);

            var status = result.ToString();

            if (string.Equals(status, "missing", StringComparison.Ordinal))
            {
                return null;
            }

            if (string.Equals(status, "dispatched", StringComparison.Ordinal) ||
                string.Equals(status, "terminal", StringComparison.Ordinal))
            {
                return await GetAsync(
                        sharedRunId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            throw new InvalidOperationException(
                $"Unexpected Redis mark-dispatched result for shared run '{sharedRunId}': '{status}'.");
        }

        /// <summary>
        /// Builds Redis script values for atomic shared run creation.
        /// </summary>
        /// <param name="record">The shared run record.</param>
        /// <param name="submittedAtScore">The submitted timestamp score.</param>
        /// <param name="expireSeconds">The optional expiration in seconds.</param>
        /// <returns>The Redis script values.</returns>
        private static RedisValue[] BuildCreateValues(
            AiSharedRunRecord record,
            long submittedAtScore,
            long expireSeconds)
        {
            var values = new List<RedisValue>
            {
                record.SharedRunId,
                submittedAtScore,
                expireSeconds
            };

            AddField(values, "sharedRunId", record.SharedRunId);
            AddField(values, "status", record.Status.ToString());
            AddField(values, "runRequestJson", Serialize(record.RunRequest));
            AddField(values, "localRunId", record.LocalRunId);
            AddField(values, "executionId", record.ExecutionId);
            AddField(values, "assignedRuntimeInstanceId", record.AssignedRuntimeInstanceId);
            AddField(values, "admissionDecisionJson", Serialize(record.AdmissionDecision));
            AddField(values, "tenantId", record.TenantId);
            AddField(values, "pipelineKey", record.PipelineKey);
            AddField(values, "correlationId", record.CorrelationId);
            AddField(values, "requestedBy", record.RequestedBy);
            AddField(values, "source", record.Source);
            AddField(values, "reason", record.Reason);
            AddField(values, "failureReason", record.FailureReason);
            AddField(values, "submittedAtUtc", record.SubmittedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            AddField(values, "updatedAtUtc", record.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            AddField(values, "metadataJson", Serialize(record.Metadata));

            return values.ToArray();
        }

        /// <summary>
        /// Adds a Redis hash field pair to a script argument list.
        /// </summary>
        /// <param name="values">The script argument list.</param>
        /// <param name="name">The hash field name.</param>
        /// <param name="value">The hash field value.</param>
        private static void AddField(
            ICollection<RedisValue> values,
            string name,
            string? value)
        {
            values.Add(name);
            values.Add(value ?? string.Empty);
        }

        /// <summary>
        /// Maps Redis hash entries to a shared run record.
        /// </summary>
        /// <param name="entries">The Redis hash entries.</param>
        /// <returns>The shared run record.</returns>
        private static AiSharedRunRecord MapRecord(
            IReadOnlyCollection<HashEntry> entries)
        {
            var fields = entries.ToDictionary(
                entry => entry.Name.ToString(),
                entry => entry.Value.ToString(),
                StringComparer.Ordinal);

            var sharedRunId = GetRequired(fields, "sharedRunId");
            var status = ParseStatus(GetRequired(fields, "status"));

            var runRequest = DeserializeRequired<AiRuntimePipelineRunRequest>(
                GetRequired(fields, "runRequestJson"),
                "runRequestJson");

            var admissionDecision = DeserializeOptional<AiRunAdmissionDecision>(
                GetOptional(fields, "admissionDecisionJson"));

            var metadata = DeserializeOptional<IReadOnlyDictionary<string, string>>(
                    GetOptional(fields, "metadataJson"))
                ?? new Dictionary<string, string>();

            return new AiSharedRunRecord
            {
                SharedRunId = sharedRunId,
                Status = status,
                RunRequest = runRequest,
                LocalRunId = GetOptional(fields, "localRunId"),
                ExecutionId = GetOptional(fields, "executionId"),
                AssignedRuntimeInstanceId = GetOptional(fields, "assignedRuntimeInstanceId"),
                AdmissionDecision = admissionDecision,
                TenantId = GetOptional(fields, "tenantId"),
                PipelineKey = GetOptional(fields, "pipelineKey"),
                CorrelationId = GetOptional(fields, "correlationId"),
                RequestedBy = GetOptional(fields, "requestedBy"),
                Source = GetOptional(fields, "source"),
                Reason = GetOptional(fields, "reason"),
                FailureReason = GetOptional(fields, "failureReason"),
                SubmittedAtUtc = ParseDateTimeOffset(GetRequired(fields, "submittedAtUtc")),
                UpdatedAtUtc = ParseDateTimeOffset(GetRequired(fields, "updatedAtUtc")),
                Metadata = metadata
            };
        }

        /// <summary>
        /// Gets an optional field value.
        /// </summary>
        /// <param name="fields">The field dictionary.</param>
        /// <param name="name">The field name.</param>
        /// <returns>The field value, or <c>null</c> when empty or missing.</returns>
        private static string? GetOptional(
            IReadOnlyDictionary<string, string> fields,
            string name)
        {
            if (!fields.TryGetValue(name, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value;
        }

        /// <summary>
        /// Gets a required field value.
        /// </summary>
        /// <param name="fields">The field dictionary.</param>
        /// <param name="name">The field name.</param>
        /// <returns>The required field value.</returns>
        private static string GetRequired(
            IReadOnlyDictionary<string, string> fields,
            string name)
        {
            if (!fields.TryGetValue(name, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Redis shared run record is missing required field '{name}'.");
            }

            return value;
        }

        /// <summary>
        /// Parses a shared run status.
        /// </summary>
        /// <param name="value">The status value.</param>
        /// <returns>The parsed shared run status.</returns>
        private static AiSharedRunStatus ParseStatus(
            string value)
        {
            if (Enum.TryParse<AiSharedRunStatus>(
                    value,
                    ignoreCase: true,
                    out var status))
            {
                return status;
            }

            return AiSharedRunStatus.Unknown;
        }

        /// <summary>
        /// Parses an ISO-8601 timestamp.
        /// </summary>
        /// <param name="value">The timestamp value.</param>
        /// <returns>The parsed timestamp.</returns>
        private static DateTimeOffset ParseDateTimeOffset(
            string value)
        {
            return DateTimeOffset.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        /// <summary>
        /// Serializes a value to JSON.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The serialized JSON, or an empty string when value is null.</returns>
        private static string Serialize<T>(
            T? value)
        {
            return value is null
                ? string.Empty
                : JsonSerializer.Serialize(value, JsonOptions);
        }

        /// <summary>
        /// Deserializes an optional JSON value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="json">The JSON value.</param>
        /// <returns>The deserialized value, or <c>null</c>.</returns>
        private static T? DeserializeOptional<T>(
            string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(
                json,
                JsonOptions);
        }

        /// <summary>
        /// Deserializes a required JSON value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="json">The JSON value.</param>
        /// <param name="fieldName">The field name used for diagnostics.</param>
        /// <returns>The deserialized value.</returns>
        private static T DeserializeRequired<T>(
            string json,
            string fieldName)
        {
            var value = JsonSerializer.Deserialize<T>(
                json,
                JsonOptions);

            if (value is null)
            {
                throw new InvalidOperationException(
                    $"Redis shared run record field '{fieldName}' could not be deserialized.");
            }

            return value;
        }

        /// <summary>
        /// Gets record expiration in seconds.
        /// </summary>
        /// <returns>The expiration in seconds, or <c>0</c> when disabled.</returns>
        private long GetExpireSeconds()
        {
            if (!_options.EnableRecordExpiration ||
                _options.RecordExpiration is null)
            {
                return 0;
            }

            return Math.Max(
                1,
                (long)_options.RecordExpiration.Value.TotalSeconds);
        }
    }
}
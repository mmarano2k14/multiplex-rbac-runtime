using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.ControlPlane.SharedQueue;
using StackExchange.Redis;
using System.Globalization;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.ControlPlane.SharedQueue
{
    /// <summary>
    /// Redis-backed implementation of the shared/global queue.
    /// </summary>
    /// <remarks>
    /// This implementation uses:
    /// - one Redis hash per shared queue item
    /// - one Redis sorted set for pending items
    /// - one Redis sorted set for all queue items
    /// - Lua scripts for atomic enqueue, claim, dispatch, requeue, and cancel operations
    ///
    /// Redis keys:
    /// - ai:shared-queue:item:{sharedRunId}
    /// - ai:shared-queue:pending
    /// - ai:shared-queue:all
    /// </remarks>
    public sealed class RedisAiSharedQueue : IAiSharedQueue
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IDatabase _database;
        private readonly RedisAiSharedQueueOptions _options;
        private readonly RedisAiSharedQueueScriptCache _scripts;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisAiSharedQueue"/> class.
        /// </summary>
        /// <param name="connection">The Redis connection multiplexer.</param>
        /// <param name="options">The Redis shared queue options.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="connection"/> or <paramref name="options"/> is null.
        /// </exception>
        public RedisAiSharedQueue(
            IConnectionMultiplexer connection,
            IOptions<RedisAiSharedQueueOptions> options)
        {
            ArgumentNullException.ThrowIfNull(connection);

            _database = connection.GetDatabase();
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _scripts = new RedisAiSharedQueueScriptCache(connection);
        }

        /// <inheritdoc />
        public async Task<AiSharedQueueItem> EnqueueAsync(
            AiSharedQueueItem item,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentException.ThrowIfNullOrWhiteSpace(item.SharedRunId);

            cancellationToken.ThrowIfCancellationRequested();

            var itemKey = _options.BuildItemKey(item.SharedRunId);
            var score = BuildQueueScore(item);
            var expireSeconds = GetExpireSeconds();

            var result = await _scripts
                .ExecuteEnqueueAsync(
                    _database,
                    new RedisKey[]
                    {
                        itemKey,
                        _options.PendingIndexKey,
                        _options.AllIndexKey
                    },
                    BuildEnqueueValues(
                        item,
                        score,
                        expireSeconds))
                .ConfigureAwait(false);

            var status = result.ToString();

            if (string.Equals(status, "duplicate", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Shared queue item '{item.SharedRunId}' already exists.");
            }

            if (!string.Equals(status, "enqueued", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected Redis enqueue result for shared queue item '{item.SharedRunId}': '{status}'.");
            }

            return item;
        }

        /// <inheritdoc />
        public async Task<AiSharedQueueItem?> GetAsync(
            string sharedRunId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sharedRunId);

            cancellationToken.ThrowIfCancellationRequested();

            var entries = await _database
                .HashGetAllAsync(_options.BuildItemKey(sharedRunId))
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (entries.Length == 0)
            {
                return null;
            }

            return MapItem(entries);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AiSharedQueueItem>> ListAsync(
            bool includeTerminal = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ids = await _database
                .SortedSetRangeByScoreAsync(
                    _options.AllIndexKey,
                    order: Order.Ascending,
                    take: _options.ListScanLimit)
                .ConfigureAwait(false);

            var items = new List<AiSharedQueueItem>();

            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sharedRunId = id.ToString();

                if (string.IsNullOrWhiteSpace(sharedRunId))
                {
                    continue;
                }

                var item = await GetAsync(
                        sharedRunId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (item is null)
                {
                    continue;
                }

                if (!includeTerminal &&
                    IsTerminal(item.Status))
                {
                    continue;
                }

                items.Add(item);
            }

            return items
                .OrderBy(item => item.Priority)
                .ThenBy(item => item.EnqueuedAtUtc)
                .ThenBy(item => item.SharedRunId, StringComparer.Ordinal)
                .ToArray();
        }

        /// <inheritdoc />
        public async Task<AiSharedQueueItem?> ClaimNextAsync(
            AiSharedQueueClaimRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.RuntimeInstanceId);

            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTimeOffset.UtcNow;
            var claimTtl = request.ClaimTtl <= TimeSpan.Zero
                ? TimeSpan.FromSeconds(30)
                : request.ClaimTtl;

            var claimToken = Guid.NewGuid().ToString("N");

            var result = await _scripts
                .ExecuteClaimNextAsync(
                    _database,
                    new RedisKey[]
                    {
                        _options.PendingIndexKey
                    },
                    new RedisValue[]
                    {
                        request.RuntimeInstanceId,
                        request.WorkerId ?? string.Empty,
                        claimToken,
                        FormatDate(now),
                        FormatDate(now.Add(claimTtl)),
                        request.TenantId ?? string.Empty,
                        request.PipelineKey ?? string.Empty,
                        request.Reason ?? string.Empty,
                        _options.KeyPrefix,
                        Math.Max(1, _options.ListScanLimit)
                    })
                .ConfigureAwait(false);

            var sharedRunId = result.ToString();

            if (string.IsNullOrWhiteSpace(sharedRunId))
            {
                return null;
            }

            return await GetAsync(
                    sharedRunId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<AiSharedQueueItem?> MarkDispatchedAsync(
            string sharedRunId,
            string claimToken,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sharedRunId);
            ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);

            cancellationToken.ThrowIfCancellationRequested();

            var result = await _scripts
                .ExecuteMarkDispatchedAsync(
                    _database,
                    new RedisKey[]
                    {
                        _options.BuildItemKey(sharedRunId)
                    },
                    new RedisValue[]
                    {
                        claimToken,
                        FormatDate(DateTimeOffset.UtcNow),
                        reason ?? string.Empty
                    })
                .ConfigureAwait(false);

            var status = result.ToString();

            if (string.Equals(status, "missing", StringComparison.Ordinal) ||
                string.Equals(status, "not-owner", StringComparison.Ordinal))
            {
                return null;
            }

            if (string.Equals(status, "dispatched", StringComparison.Ordinal))
            {
                return await GetAsync(
                        sharedRunId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            throw new InvalidOperationException(
                $"Unexpected Redis dispatch result for shared queue item '{sharedRunId}': '{status}'.");
        }

        /// <inheritdoc />
        public async Task<AiSharedQueueItem?> RequeueAsync(
            string sharedRunId,
            string claimToken,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sharedRunId);
            ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);

            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTimeOffset.UtcNow;

            var result = await _scripts
                .ExecuteRequeueAsync(
                    _database,
                    new RedisKey[]
                    {
                        _options.BuildItemKey(sharedRunId),
                        _options.PendingIndexKey
                    },
                    new RedisValue[]
                    {
                        sharedRunId,
                        claimToken,
                        BuildQueueScoreFromParts(
                            priority: 0,
                            enqueuedAtUtc: now),
                        FormatDate(now),
                        reason ?? string.Empty
                    })
                .ConfigureAwait(false);

            var status = result.ToString();

            if (string.Equals(status, "missing", StringComparison.Ordinal) ||
                string.Equals(status, "not-owner", StringComparison.Ordinal))
            {
                return null;
            }

            if (string.Equals(status, "requeued", StringComparison.Ordinal))
            {
                return await GetAsync(
                        sharedRunId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            throw new InvalidOperationException(
                $"Unexpected Redis requeue result for shared queue item '{sharedRunId}': '{status}'.");
        }

        /// <inheritdoc />
        public async Task<AiSharedQueueItem?> CancelAsync(
            string sharedRunId,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sharedRunId);

            cancellationToken.ThrowIfCancellationRequested();

            var result = await _scripts
                .ExecuteCancelAsync(
                    _database,
                    new RedisKey[]
                    {
                        _options.BuildItemKey(sharedRunId),
                        _options.PendingIndexKey
                    },
                    new RedisValue[]
                    {
                        sharedRunId,
                        FormatDate(DateTimeOffset.UtcNow),
                        reason ?? "Shared queue item cancelled."
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
                $"Unexpected Redis cancel result for shared queue item '{sharedRunId}': '{status}'.");
        }

        /// <summary>
        /// Builds Redis script values for enqueue.
        /// </summary>
        private static RedisValue[] BuildEnqueueValues(
            AiSharedQueueItem item,
            double score,
            long expireSeconds)
        {
            var values = new List<RedisValue>
            {
                item.SharedRunId,
                score.ToString(CultureInfo.InvariantCulture),
                expireSeconds
            };

            AddField(values, "sharedRunId", item.SharedRunId);
            AddField(values, "status", item.Status.ToString());
            AddField(values, "tenantId", item.TenantId);
            AddField(values, "pipelineKey", item.PipelineKey);
            AddField(values, "priority", item.Priority.ToString(CultureInfo.InvariantCulture));
            AddField(values, "claimedByRuntimeInstanceId", item.ClaimedByRuntimeInstanceId);
            AddField(values, "claimedByWorkerId", item.ClaimedByWorkerId);
            AddField(values, "claimToken", item.ClaimToken);
            AddField(values, "enqueuedAtUtc", FormatDate(item.EnqueuedAtUtc));
            AddField(values, "updatedAtUtc", FormatDate(item.UpdatedAtUtc));
            AddField(values, "claimedAtUtc", FormatOptionalDate(item.ClaimedAtUtc));
            AddField(values, "claimExpiresAtUtc", FormatOptionalDate(item.ClaimExpiresAtUtc));
            AddField(values, "reason", item.Reason);
            AddField(values, "metadataJson", Serialize(item.Metadata));

            return values.ToArray();
        }

        /// <summary>
        /// Adds a Redis hash field pair to a script argument list.
        /// </summary>
        private static void AddField(
            ICollection<RedisValue> values,
            string name,
            string? value)
        {
            values.Add(name);
            values.Add(value ?? string.Empty);
        }

        /// <summary>
        /// Maps Redis hash entries to a shared queue item.
        /// </summary>
        private static AiSharedQueueItem MapItem(
            IReadOnlyCollection<HashEntry> entries)
        {
            var fields = entries.ToDictionary(
                entry => entry.Name.ToString(),
                entry => entry.Value.ToString(),
                StringComparer.Ordinal);

            var metadata = DeserializeOptional<IReadOnlyDictionary<string, string>>(
                    GetOptional(fields, "metadataJson"))
                ?? new Dictionary<string, string>();

            return new AiSharedQueueItem
            {
                SharedRunId = GetRequired(fields, "sharedRunId"),
                Status = ParseStatus(GetRequired(fields, "status")),
                TenantId = GetOptional(fields, "tenantId"),
                PipelineKey = GetOptional(fields, "pipelineKey"),
                Priority = ParseInt(GetOptional(fields, "priority")),
                ClaimedByRuntimeInstanceId = GetOptional(fields, "claimedByRuntimeInstanceId"),
                ClaimedByWorkerId = GetOptional(fields, "claimedByWorkerId"),
                ClaimToken = GetOptional(fields, "claimToken"),
                EnqueuedAtUtc = ParseDateTimeOffset(GetRequired(fields, "enqueuedAtUtc")),
                UpdatedAtUtc = ParseDateTimeOffset(GetRequired(fields, "updatedAtUtc")),
                ClaimedAtUtc = ParseOptionalDateTimeOffset(GetOptional(fields, "claimedAtUtc")),
                ClaimExpiresAtUtc = ParseOptionalDateTimeOffset(GetOptional(fields, "claimExpiresAtUtc")),
                Reason = GetOptional(fields, "reason"),
                Metadata = metadata
            };
        }

        /// <summary>
        /// Gets an optional field value.
        /// </summary>
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
        private static string GetRequired(
            IReadOnlyDictionary<string, string> fields,
            string name)
        {
            if (!fields.TryGetValue(name, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Redis shared queue item is missing required field '{name}'.");
            }

            return value;
        }

        /// <summary>
        /// Parses a shared queue item status.
        /// </summary>
        private static AiSharedQueueItemStatus ParseStatus(
            string value)
        {
            if (Enum.TryParse<AiSharedQueueItemStatus>(
                    value,
                    ignoreCase: true,
                    out var status))
            {
                return status;
            }

            return AiSharedQueueItemStatus.Unknown;
        }

        /// <summary>
        /// Parses an integer value.
        /// </summary>
        private static int ParseInt(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed)
                    ? parsed
                    : 0;
        }

        /// <summary>
        /// Formats a timestamp as round-trip ISO-8601.
        /// </summary>
        private static string FormatDate(
            DateTimeOffset value)
        {
            return value.ToString("O", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats an optional timestamp as round-trip ISO-8601.
        /// </summary>
        private static string? FormatOptionalDate(
            DateTimeOffset? value)
        {
            return value?.ToString("O", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses an ISO-8601 timestamp.
        /// </summary>
        private static DateTimeOffset ParseDateTimeOffset(
            string value)
        {
            return DateTimeOffset.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        /// <summary>
        /// Parses an optional ISO-8601 timestamp.
        /// </summary>
        private static DateTimeOffset? ParseOptionalDateTimeOffset(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return ParseDateTimeOffset(value);
        }

        /// <summary>
        /// Serializes a value to JSON.
        /// </summary>
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
        /// Determines whether a queue item status is terminal.
        /// </summary>
        private static bool IsTerminal(
            AiSharedQueueItemStatus status)
        {
            return status is
                AiSharedQueueItemStatus.Completed or
                AiSharedQueueItemStatus.Failed or
                AiSharedQueueItemStatus.Cancelled or
                AiSharedQueueItemStatus.Dispatched;
        }

        /// <summary>
        /// Builds a Redis sorted set score for queue ordering.
        /// </summary>
        private static double BuildQueueScore(
            AiSharedQueueItem item)
        {
            return BuildQueueScoreFromParts(
                item.Priority,
                item.EnqueuedAtUtc);
        }

        /// <summary>
        /// Builds a Redis sorted set score from priority and enqueue timestamp.
        /// </summary>
        private static double BuildQueueScoreFromParts(
            int priority,
            DateTimeOffset enqueuedAtUtc)
        {
            var timestamp = enqueuedAtUtc.ToUnixTimeMilliseconds();

            return (priority * 1_000_000_000_000d) + timestamp;
        }

        /// <summary>
        /// Gets record expiration in seconds.
        /// </summary>
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
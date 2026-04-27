using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Payloads.Models;
using Multiplexed.Abstractions.AI.Execution.Payloads.Resolvers;
using Multiplexed.Abstractions.AI.Execution.State;
using Multiplexed.AI.Runtime.Execution.State;
using System.Text.Json;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Execution
{
    /// <summary>
    /// Validates execution state read/write behavior through the dedicated
    /// state reader and writer services.
    ///
    /// PURPOSE:
    /// - AiExecutionState is now a durable persistence model only.
    /// - IAiExecutionStateWriter owns mutation.
    /// - IAiExecutionStateReader owns read/conversion behavior.
    ///
    /// IMPORTANT:
    /// - Tests must not call removed state methods such as state.Get or state.Set.
    /// - Inline reads still go through the reader.
    /// - Inline writes still go through the writer.
    /// </summary>
    public sealed class AiExecutionStateTests
    {
        private readonly IAiExecutionStateWriter _writer = new DefaultAiExecutionStateWriter();
        private readonly IAiExecutionStateReader _reader = new DefaultAiExecutionStateReader(
            new NoopPayloadResolver());

        [Fact]
        public async Task GetDataAsync_Should_Return_Default_When_Key_Does_Not_Exist()
        {
            var state = new AiExecutionState();

            var value = await _reader.GetDataAsync<string>(state, "missing");

            Assert.Null(value);
        }

        [Fact]
        public async Task GetDataAsync_Should_Return_Typed_Value_When_Value_Is_Stored_Directly()
        {
            var state = new AiExecutionState();

            _writer.SetData(state, "input", "hello");

            var value = await _reader.GetDataAsync<string>(state, "input");

            Assert.Equal("hello", value);
        }

        [Fact]
        public async Task GetDataAsync_Should_Return_Default_When_Value_Is_Null()
        {
            var state = new AiExecutionState
            {
                Data =
                {
                    ["input"] = null
                }
            };

            var value = await _reader.GetDataAsync<string>(state, "input");

            Assert.Null(value);
        }

        [Fact]
        public async Task GetDataAsync_Should_Read_String_From_JsonElement_After_Json_RoundTrip()
        {
            var state = new AiExecutionState();

            _writer.SetData(state, "input", "hello world");

            var restored = RoundTrip(state);

            var value = await _reader.GetDataAsync<string>(restored, "input");

            Assert.Equal("hello world", value);
        }

        [Fact]
        public async Task GetDataAsync_Should_Read_Int_From_JsonElement_After_Json_RoundTrip()
        {
            var state = new AiExecutionState();

            _writer.SetData(state, "count", 42);

            var restored = RoundTrip(state);

            var value = await _reader.GetDataAsync<int>(restored, "count");

            Assert.Equal(42, value);
        }

        [Fact]
        public async Task GetDataAsync_Should_Read_Bool_From_JsonElement_After_Json_RoundTrip()
        {
            var state = new AiExecutionState();

            _writer.SetData(state, "flag", true);

            var restored = RoundTrip(state);

            var value = await _reader.GetDataAsync<bool>(restored, "flag");

            Assert.True(value);
        }

        [Fact]
        public async Task GetDataAsync_Should_Read_Object_From_JsonElement_After_Json_RoundTrip()
        {
            var state = new AiExecutionState();

            _writer.SetData(state, "payload", new TestPayload
            {
                Name = "Marco",
                Count = 3
            });

            var restored = RoundTrip(state);

            var value = await _reader.GetDataAsync<TestPayload>(restored, "payload");

            Assert.NotNull(value);
            Assert.Equal("Marco", value!.Name);
            Assert.Equal(3, value.Count);
        }

        [Fact]
        public void Writer_SetData_Should_Update_Timestamp()
        {
            var state = new AiExecutionState();
            var before = state.UpdatedAtUtc;

            Thread.Sleep(5);

            _writer.SetData(state, "input", "hello");

            Assert.True(state.UpdatedAtUtc > before);
        }

        [Fact]
        public void Writer_RemoveData_Should_Remove_Key_And_Update_Timestamp()
        {
            var state = new AiExecutionState();

            _writer.SetData(state, "input", "hello");

            var before = state.UpdatedAtUtc;

            Thread.Sleep(5);

            var removed = _writer.RemoveData(state, "input");

            Assert.True(removed);
            Assert.False(state.Data.ContainsKey("input"));
            Assert.True(state.UpdatedAtUtc > before);
        }

        [Fact]
        public void Writer_RemoveData_Should_Return_False_When_Key_Does_Not_Exist()
        {
            var state = new AiExecutionState();

            var removed = _writer.RemoveData(state, "missing");

            Assert.False(removed);
        }

        [Fact]
        public async Task GetMetadataAsync_Should_Return_Typed_Value_When_Value_Is_Stored_Directly()
        {
            var state = new AiExecutionState();

            _writer.SetMetadata(state, "traceId", "abc-123");

            var value = await _reader.GetMetadataAsync<string>(state, "traceId");

            Assert.Equal("abc-123", value);
        }

        [Fact]
        public async Task GetMetadataAsync_Should_Read_String_From_JsonElement_After_Json_RoundTrip()
        {
            var state = new AiExecutionState();

            _writer.SetMetadata(state, "traceId", "abc-123");

            var restored = RoundTrip(state);

            var value = await _reader.GetMetadataAsync<string>(restored, "traceId");

            Assert.Equal("abc-123", value);
        }

        [Fact]
        public void Writer_SetMetadata_Should_Update_Timestamp()
        {
            var state = new AiExecutionState();
            var before = state.UpdatedAtUtc;

            Thread.Sleep(5);

            _writer.SetMetadata(state, "traceId", "abc-123");

            Assert.True(state.UpdatedAtUtc > before);
        }

        [Fact]
        public void Writer_RemoveMetadata_Should_Remove_Key_And_Update_Timestamp()
        {
            var state = new AiExecutionState();

            _writer.SetMetadata(state, "traceId", "abc-123");

            var before = state.UpdatedAtUtc;

            Thread.Sleep(5);

            var removed = _writer.RemoveMetadata(state, "traceId");

            Assert.True(removed);
            Assert.False(state.Metadata.ContainsKey("traceId"));
            Assert.True(state.UpdatedAtUtc > before);
        }

        [Fact]
        public void Writer_RemoveMetadata_Should_Return_False_When_Key_Does_Not_Exist()
        {
            var state = new AiExecutionState();

            var removed = _writer.RemoveMetadata(state, "missing");

            Assert.False(removed);
        }

        /// <summary>
        /// Performs a JSON round-trip to simulate persistence restore behavior.
        /// </summary>
        private static AiExecutionState RoundTrip(AiExecutionState state)
        {
            var json = JsonSerializer.Serialize(state);
            var restored = JsonSerializer.Deserialize<AiExecutionState>(json);

            Assert.NotNull(restored);

            return restored!;
        }

        /// <summary>
        /// Minimal payload resolver used by tests that only exercise inline state values.
        /// </summary>
        private sealed class NoopPayloadResolver : IAiExecutionPayloadResolver
        {
            public Task<object?> ResolveAsync(
                AiStoredPayload payload,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException(
                    "This test resolver should not be used for inline-only state tests.");
            }
        }

        private sealed class TestPayload
        {
            public string Name { get; set; } = string.Empty;

            public int Count { get; set; }
        }
    }
}
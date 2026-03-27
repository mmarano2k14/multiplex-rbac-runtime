using System.Text.Json;
using Multiplexed.Abstractions.AI.Execution;
using Xunit;

namespace Multiplexed.AI.Tests.Runtime.Execution
{
    public sealed class AiExecutionStateTests
    {
        [Fact]
        public void Get_Should_Return_Default_When_Key_Does_Not_Exist()
        {
            // Arrange
            var state = new AiExecutionState();

            // Act
            var value = state.Get<string>("missing");

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public void Get_Should_Return_Typed_Value_When_Value_Is_Stored_Directly()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Set("input", "hello");

            // Act
            var value = state.Get<string>("input");

            // Assert
            Assert.Equal("hello", value);
        }

        [Fact]
        public void Get_Should_Return_Default_When_Value_Is_Null()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Data["input"] = null;

            // Act
            var value = state.Get<string>("input");

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public void Get_Should_Throw_When_Value_Type_Does_Not_Match()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Set("count", 42);

            // Act
            var exception = Assert.Throws<InvalidCastException>(() => state.Get<string>("count"));

            // Assert
            Assert.Contains("count", exception.Message);
            Assert.Contains("Int32", exception.Message);
            Assert.Contains("String", exception.Message);
        }

        [Fact]
        public void Get_Should_Read_String_From_JsonElement_After_Json_RoundTrip()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Set("input", "hello world");

            var json = JsonSerializer.Serialize(state);
            var restored = JsonSerializer.Deserialize<AiExecutionState>(json);

            Assert.NotNull(restored);

            // Act
            var value = restored!.Get<string>("input");

            // Assert
            Assert.Equal("hello world", value);
        }

        [Fact]
        public void Get_Should_Read_Int_From_JsonElement_After_Json_RoundTrip()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Set("count", 42);

            var json = JsonSerializer.Serialize(state);
            var restored = JsonSerializer.Deserialize<AiExecutionState>(json);

            Assert.NotNull(restored);

            // Act
            var value = restored!.Get<int>("count");

            // Assert
            Assert.Equal(42, value);
        }

        [Fact]
        public void Get_Should_Read_Bool_From_JsonElement_After_Json_RoundTrip()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Set("flag", true);

            var json = JsonSerializer.Serialize(state);
            var restored = JsonSerializer.Deserialize<AiExecutionState>(json);

            Assert.NotNull(restored);

            // Act
            var value = restored!.Get<bool>("flag");

            // Assert
            Assert.True(value);
        }

        [Fact]
        public void Get_Should_Read_Object_From_JsonElement_After_Json_RoundTrip()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Set("payload", new TestPayload
            {
                Name = "Marco",
                Count = 3
            });

            var json = JsonSerializer.Serialize(state);
            var restored = JsonSerializer.Deserialize<AiExecutionState>(json);

            Assert.NotNull(restored);

            // Act
            var value = restored!.Get<TestPayload>("payload");

            // Assert
            Assert.NotNull(value);
            Assert.Equal("Marco", value!.Name);
            Assert.Equal(3, value.Count);
        }

        [Fact]
        public void TryGet_Should_Return_True_When_Typed_Value_Exists()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Set("input", "hello");

            // Act
            var found = state.TryGet<string>("input", out var value);

            // Assert
            Assert.True(found);
            Assert.Equal("hello", value);
        }

        [Fact]
        public void TryGet_Should_Return_True_When_Null_Value_Exists()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Data["input"] = null;

            // Act
            var found = state.TryGet<string>("input", out var value);

            // Assert
            Assert.True(found);
            Assert.Null(value);
        }

        [Fact]
        public void TryGet_Should_Return_False_When_Key_Does_Not_Exist()
        {
            // Arrange
            var state = new AiExecutionState();

            // Act
            var found = state.TryGet<string>("missing", out var value);

            // Assert
            Assert.False(found);
            Assert.Null(value);
        }

        [Fact]
        public void TryGet_Should_Return_True_For_String_From_JsonElement_After_Json_RoundTrip()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Set("branch", "update-1");

            var json = JsonSerializer.Serialize(state);
            var restored = JsonSerializer.Deserialize<AiExecutionState>(json);

            Assert.NotNull(restored);

            // Act
            var found = restored!.TryGet<string>("branch", out var value);

            // Assert
            Assert.True(found);
            Assert.Equal("update-1", value);
        }

        [Fact]
        public void TryGet_Should_Return_False_When_JsonElement_Cannot_Be_Converted()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Set("count", 42);

            var json = JsonSerializer.Serialize(state);
            var restored = JsonSerializer.Deserialize<AiExecutionState>(json);

            Assert.NotNull(restored);

            // Act
            var found = restored!.TryGet<DateTime>("count", out var value);

            // Assert
            Assert.False(found);
            Assert.Equal(default, value);
        }

        [Fact]
        public void Contains_Should_Return_True_When_Key_Exists()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Set("input", "hello");

            // Act
            var contains = state.Contains("input");

            // Assert
            Assert.True(contains);
        }

        [Fact]
        public void Contains_Should_Return_False_When_Key_Does_Not_Exist()
        {
            // Arrange
            var state = new AiExecutionState();

            // Act
            var contains = state.Contains("missing");

            // Assert
            Assert.False(contains);
        }

        [Fact]
        public void Remove_Should_Remove_Key_And_Update_Timestamp()
        {
            // Arrange
            var state = new AiExecutionState();
            state.Set("input", "hello");
            var before = state.UpdatedAtUtc;

            Thread.Sleep(5);

            // Act
            state.Remove("input");

            // Assert
            Assert.False(state.Contains("input"));
            Assert.True(state.UpdatedAtUtc > before);
        }

        [Fact]
        public void Set_Should_Update_Timestamp()
        {
            // Arrange
            var state = new AiExecutionState();
            var before = state.UpdatedAtUtc;

            Thread.Sleep(5);

            // Act
            state.Set("input", "hello");

            // Assert
            Assert.True(state.UpdatedAtUtc > before);
        }

        [Fact]
        public void GetMetadata_Should_Return_Typed_Value_When_Value_Is_Stored_Directly()
        {
            // Arrange
            var state = new AiExecutionState();
            state.SetMetadata("traceId", "abc-123");

            // Act
            var value = state.GetMetadata<string>("traceId");

            // Assert
            Assert.Equal("abc-123", value);
        }

        [Fact]
        public void GetMetadata_Should_Read_String_From_JsonElement_After_Json_RoundTrip()
        {
            // Arrange
            var state = new AiExecutionState();
            state.SetMetadata("traceId", "abc-123");

            var json = JsonSerializer.Serialize(state);
            var restored = JsonSerializer.Deserialize<AiExecutionState>(json);

            Assert.NotNull(restored);

            // Act
            var value = restored!.GetMetadata<string>("traceId");

            // Assert
            Assert.Equal("abc-123", value);
        }

        [Fact]
        public void TryGetMetadata_Should_Return_True_When_Value_Exists()
        {
            // Arrange
            var state = new AiExecutionState();
            state.SetMetadata("traceId", "abc-123");

            // Act
            var found = state.TryGetMetadata<string>("traceId", out var value);

            // Assert
            Assert.True(found);
            Assert.Equal("abc-123", value);
        }

        [Fact]
        public void ContainsMetadata_Should_Return_True_When_Key_Exists()
        {
            // Arrange
            var state = new AiExecutionState();
            state.SetMetadata("traceId", "abc-123");

            // Act
            var contains = state.ContainsMetadata("traceId");

            // Assert
            Assert.True(contains);
        }

        [Fact]
        public void RemoveMetadata_Should_Remove_Key_And_Update_Timestamp()
        {
            // Arrange
            var state = new AiExecutionState();
            state.SetMetadata("traceId", "abc-123");
            var before = state.UpdatedAtUtc;

            Thread.Sleep(5);

            // Act
            state.RemoveMetadata("traceId");

            // Assert
            Assert.False(state.ContainsMetadata("traceId"));
            Assert.True(state.UpdatedAtUtc > before);
        }

        private sealed class TestPayload
        {
            public string Name { get; set; } = string.Empty;
            public int Count { get; set; }
        }
    }
}
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.ControlPlane.ExecutionAssistance;
using Multiplexed.AI.Runtime.ControlPlane.ExecutionAssistance;
using Xunit;

namespace Multiplexed.AI.Tests.Unit.ControlPlane.ExecutionAssistance
{
    /// <summary>
    /// Unit tests for <see cref="AiExecutionAssistanceController"/>.
    /// </summary>
    public sealed class AiExecutionAssistanceControllerTests
    {
        /// <summary>
        /// Validates that assistance is denied when execution assistance is disabled.
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_Should_Deny_When_Assistance_Is_Disabled()
        {
            var controller = CreateController(
                options => options.Enabled = false);

            var decision = await controller.EvaluateAsync(
                CreateValidRequest());

            Assert.False(decision.Allowed);
            Assert.Null(decision.Lease);
            Assert.Equal(
                "Execution assistance is disabled.",
                decision.Reason);
        }

        /// <summary>
        /// Validates that assistance is denied when the helper runtime instance
        /// is the same as the primary runtime instance.
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_Should_Deny_When_Helper_Is_Primary_Instance()
        {
            var controller = CreateController();

            var decision = await controller.EvaluateAsync(
                CreateValidRequest(
                    primaryRuntimeInstanceId: "runtime-instance-1",
                    helperRuntimeInstanceId: "runtime-instance-1"));

            Assert.False(decision.Allowed);
            Assert.Null(decision.Lease);
            Assert.Equal(
                "The primary runtime instance cannot be registered as a helper for its own execution.",
                decision.Reason);
        }

        /// <summary>
        /// Validates that assistance is denied when the helper runtime instance
        /// is not idle and the policy requires idle helpers.
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_Should_Deny_When_Helper_Is_Not_Idle()
        {
            var controller = CreateController(
                options => options.OnlyWhenLocalQueueIdle = true);

            var decision = await controller.EvaluateAsync(
                CreateValidRequest(
                    helperIsIdle: false));

            Assert.False(decision.Allowed);
            Assert.Null(decision.Lease);
            Assert.Equal(
                "The helper runtime instance is not idle.",
                decision.Reason);
        }

        /// <summary>
        /// Validates that assistance is denied when the helper runtime instance
        /// queue depth is above the configured threshold.
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_Should_Deny_When_Helper_Queue_Depth_Is_Too_High()
        {
            var controller = CreateController(
                options =>
                {
                    options.OnlyWhenLocalQueueIdle = false;
                    options.MaxHelperQueueDepth = 1;
                });

            var decision = await controller.EvaluateAsync(
                CreateValidRequest(
                    helperQueueDepth: 2));

            Assert.False(decision.Allowed);
            Assert.Null(decision.Lease);
            Assert.Equal(
                "The helper runtime instance queue depth is above the allowed assistance threshold.",
                decision.Reason);
        }

        /// <summary>
        /// Validates that assistance is denied when the helper runtime instance
        /// has no available worker slots.
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_Should_Deny_When_Helper_Has_No_Available_Worker_Slots()
        {
            var controller = CreateController();

            var decision = await controller.EvaluateAsync(
                CreateValidRequest(
                    helperAvailableWorkerSlots: 0));

            Assert.False(decision.Allowed);
            Assert.Null(decision.Lease);
            Assert.Equal(
                "The helper runtime instance has no available worker slots.",
                decision.Reason);
        }

        /// <summary>
        /// Validates that assistance is denied when the execution does not have
        /// enough ready steps to justify helper participation.
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_Should_Deny_When_Not_Enough_Ready_Steps()
        {
            var controller = CreateController(
                options => options.MinReadyStepsToAssist = 10);

            var decision = await controller.EvaluateAsync(
                CreateValidRequest(
                    readyStepCount: 9));

            Assert.False(decision.Allowed);
            Assert.Null(decision.Lease);
            Assert.Equal(
                "The execution does not have enough ready steps to justify assistance.",
                decision.Reason);
        }

        /// <summary>
        /// Validates that assistance is denied when the execution does not have
        /// enough remaining non-terminal steps.
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_Should_Deny_When_Not_Enough_Remaining_Steps()
        {
            var controller = CreateController(
                options => options.MinRemainingStepsToAssist = 25);

            var decision = await controller.EvaluateAsync(
                CreateValidRequest(
                    remainingStepCount: 24));

            Assert.False(decision.Allowed);
            Assert.Null(decision.Lease);
            Assert.Equal(
                "The execution does not have enough remaining work to justify assistance.",
                decision.Reason);
        }

        /// <summary>
        /// Validates that assistance is denied when the execution already has the
        /// maximum number of helper runtime instances.
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_Should_Deny_When_Max_Helper_Count_Reached()
        {
            var controller = CreateController(
                options => options.MaxHelpersPerExecution = 2);

            var decision = await controller.EvaluateAsync(
                CreateValidRequest(
                    activeHelperCount: 2));

            Assert.False(decision.Allowed);
            Assert.Null(decision.Lease);
            Assert.Equal(
                "The execution already has the maximum number of helper runtime instances.",
                decision.Reason);
        }

        /// <summary>
        /// Validates that assistance is denied when the execution already reached
        /// the maximum worker budget.
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_Should_Deny_When_Max_Worker_Budget_Reached()
        {
            var controller = CreateController(
                options => options.MaxWorkersPerExecution = 12);

            var decision = await controller.EvaluateAsync(
                CreateValidRequest(
                    activeWorkerCountForExecution: 12));

            Assert.False(decision.Allowed);
            Assert.Null(decision.Lease);
            Assert.Equal(
                "The execution already reached the maximum worker budget.",
                decision.Reason);
        }

        /// <summary>
        /// Validates that assistance is granted when all assistance requirements are met.
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_Should_Grant_Lease_When_Request_Is_Valid()
        {
            var store = new InMemoryAiExecutionAssistanceStore();

            var controller = CreateController(
                store,
                options =>
                {
                    options.Enabled = true;
                    options.MaxHelpersPerExecution = 2;
                    options.MaxWorkersPerExecution = 12;
                    options.MaxWorkersPerHelperInstance = 3;
                    options.MinReadyStepsToAssist = 10;
                    options.MinRemainingStepsToAssist = 25;
                    options.OnlyWhenLocalQueueIdle = true;
                    options.MaxHelperQueueDepth = 0;
                    options.LeaseTtl = TimeSpan.FromSeconds(30);
                });

            var decision = await controller.EvaluateAsync(
                CreateValidRequest(
                    helperAvailableWorkerSlots: 5,
                    activeWorkerCountForExecution: 4));

            Assert.True(decision.Allowed);
            Assert.NotNull(decision.Lease);

            Assert.Equal("execution-1", decision.ExecutionId);
            Assert.Equal("runtime-instance-1", decision.PrimaryRuntimeInstanceId);
            Assert.Equal("runtime-instance-2", decision.HelperRuntimeInstanceId);

            Assert.Equal("execution-1", decision.Lease!.ExecutionId);
            Assert.Equal("runtime-instance-1", decision.Lease.PrimaryRuntimeInstanceId);
            Assert.Equal("runtime-instance-2", decision.Lease.HelperRuntimeInstanceId);
            Assert.Equal(AiExecutionAssistanceStatus.Granted, decision.Lease.Status);

            Assert.Equal(3, decision.Lease.MaxWorkers);

            var storedLease = await store.GetAsync(
                decision.Lease.LeaseId);

            Assert.NotNull(storedLease);
            Assert.Equal(decision.Lease.LeaseId, storedLease!.LeaseId);
        }

        /// <summary>
        /// Validates that the granted lease worker count is capped by the remaining
        /// execution worker budget.
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_Should_Cap_Lease_Workers_By_Remaining_Execution_Budget()
        {
            var controller = CreateController(
                options =>
                {
                    options.MaxWorkersPerExecution = 12;
                    options.MaxWorkersPerHelperInstance = 5;
                });

            var decision = await controller.EvaluateAsync(
                CreateValidRequest(
                    helperAvailableWorkerSlots: 10,
                    activeWorkerCountForExecution: 10));

            Assert.True(decision.Allowed);
            Assert.NotNull(decision.Lease);
            Assert.Equal(2, decision.Lease!.MaxWorkers);
        }

        private static AiExecutionAssistanceController CreateController(
            Action<AiExecutionAssistanceOptions>? configureOptions = null)
        {
            return CreateController(
                new InMemoryAiExecutionAssistanceStore(),
                configureOptions);
        }

        private static AiExecutionAssistanceController CreateController(
            IAiExecutionAssistanceStore store,
            Action<AiExecutionAssistanceOptions>? configureOptions = null)
        {
            var options = new AiExecutionAssistanceOptions
            {
                Enabled = true,
                MaxHelpersPerExecution = 2,
                MaxWorkersPerExecution = 12,
                MaxWorkersPerHelperInstance = 2,
                MinReadyStepsToAssist = 10,
                MinRemainingStepsToAssist = 25,
                OnlyWhenLocalQueueIdle = true,
                MaxHelperQueueDepth = 0,
                LeaseTtl = TimeSpan.FromSeconds(30)
            };

            configureOptions?.Invoke(options);

            return new AiExecutionAssistanceController(
                store,
                Options.Create(options));
        }

        private static AiExecutionAssistanceRequest CreateValidRequest(
            string executionId = "execution-1",
            string primaryRuntimeInstanceId = "runtime-instance-1",
            string helperRuntimeInstanceId = "runtime-instance-2",
            int readyStepCount = 50,
            int remainingStepCount = 100,
            int activeHelperCount = 0,
            int activeWorkerCountForExecution = 2,
            bool helperIsIdle = true,
            int helperQueueDepth = 0,
            int helperAvailableWorkerSlots = 4)
        {
            return new AiExecutionAssistanceRequest
            {
                ExecutionId = executionId,
                PrimaryRuntimeInstanceId = primaryRuntimeInstanceId,
                HelperRuntimeInstanceId = helperRuntimeInstanceId,
                ReadyStepCount = readyStepCount,
                RemainingStepCount = remainingStepCount,
                ActiveHelperCount = activeHelperCount,
                ActiveWorkerCountForExecution = activeWorkerCountForExecution,
                HelperIsIdle = helperIsIdle,
                HelperQueueDepth = helperQueueDepth,
                HelperAvailableWorkerSlots = helperAvailableWorkerSlots,
                CorrelationId = "correlation-1",
                RequestedBy = "unit-test",
                Source = "execution-assistance-controller-tests",
                Reason = "Validate execution assistance decision logic.",
                Metadata = new Dictionary<string, string>
                {
                    ["test"] = "true"
                }
            };
        }
    }
}
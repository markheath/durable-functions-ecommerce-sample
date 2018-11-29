using System;
using System.Threading.Tasks;
using DurableECommerceWorkflow;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace DurableECommerceTests
{
    public class OrchestratorFunctionTests
    {
        private ILogger mockLogger;

        [SetUp]
        public void Setup()
        {
            mockLogger = Mock.Of<ILogger>();
        }

        [Test]
        public async Task CanSuccessfullyProcessAnOrder()
        {
            var order = CreateTestOrder(false);
            var context = CreateMockDurableOrchestrationContext(order, null);

            var orderResult = await OrchestratorFunctions.ProcessOrder(context.Object, mockLogger);

            context.Verify(c => c.CallActivityAsync("A_SaveOrderToDatabase", order), Times.Once);
            context.Verify(c => c.CallActivityAsync("A_RequestOrderApproval", order), Times.Never);
            context.Verify(c => c.CallActivityAsync<string>("A_CreatePersonalizedPdf", order), Times.Once);
            context.Verify(c => c.CallActivityAsync<string>("A_CreateWatermarkedVideo", order), Times.Once);

            context.Verify(c => c.CallActivityWithRetryAsync("A_SendOrderConfirmationEmail",
                It.IsAny<RetryOptions>(),
                It.IsAny<object>()), Times.Once);

            Assert.That(orderResult, Is.EqualTo(new OrderResult { Status = "Success", Pdf = "example.pdf", Video = "example.mp4" }).Using<OrderResult, OrderResult>(CompareOrderResult));
        }

        [Test]
        public async Task CanSuccessfullyProcessAnOrderWithApproval()
        {
            var order = CreateTestOrder(true);
            var context = CreateMockDurableOrchestrationContext(order, "Approved");

            var orderResult = await OrchestratorFunctions.ProcessOrder(context.Object, mockLogger);

            context.Verify(c => c.CallActivityAsync("A_SaveOrderToDatabase", order), Times.Once);
            context.Verify(c => c.CallActivityAsync("A_RequestOrderApproval", order), Times.Once);
            context.Verify(c => c.CallActivityAsync<string>("A_CreatePersonalizedPdf", order), Times.Once);
            context.Verify(c => c.CallActivityAsync<string>("A_CreateWatermarkedVideo", order), Times.Once);

            context.Verify(c => c.CallActivityWithRetryAsync("A_SendOrderConfirmationEmail",
                It.IsAny<RetryOptions>(),
                It.IsAny<object>()), Times.Once);

            Assert.That(orderResult, Is.EqualTo(new OrderResult { Status = "Success", Pdf = "example.pdf", Video = "example.mp4"}).Using<OrderResult, OrderResult>(CompareOrderResult));
        }


        [Test]
        public async Task CanNotifyAnOrderNotApproved()
        {
            var order = CreateTestOrder(true);
            var context = CreateMockDurableOrchestrationContext(order, "Rejected");

            var orderResult = await OrchestratorFunctions.ProcessOrder(context.Object, mockLogger);

            context.Verify(c => c.CallActivityAsync("A_SaveOrderToDatabase", order), Times.Once);
            context.Verify(c => c.CallActivityAsync("A_RequestOrderApproval", order), Times.Once);
            context.Verify(c => c.CallActivityAsync("A_SendNotApprovedEmail", order), Times.Once);

            Assert.That(orderResult, Is.EqualTo(new OrderResult { Status = "NotApproved"}).Using<OrderResult,OrderResult>(CompareOrderResult));
        }

        [Test]
        public async Task SendsProblemEmailOnFailureToTranscode()
        {
            var order = CreateTestOrder(false);
            var context = CreateMockDurableOrchestrationContext(order, null, true);

            var orderResult = await OrchestratorFunctions.ProcessOrder(context.Object, mockLogger);

            context.Verify(c => c.CallActivityAsync("A_SaveOrderToDatabase", order), Times.Once);
            context.Verify(c => c.CallActivityAsync("A_RequestOrderApproval", order), Times.Never);
            context.Verify(c => c.CallActivityWithRetryAsync("A_SendProblemEmail", It.IsAny<RetryOptions>(), order), Times.Once);

            Assert.That(orderResult, Is.EqualTo(new OrderResult { Status = "Problem" }).Using<OrderResult, OrderResult>(CompareOrderResult));

        }

        private static Order CreateTestOrder(bool requiresApproval)
        {
            var order = new Order
            {
                Amount = requiresApproval ? 12345 : 50,
                Id = "102030",
                OrchestrationId = "100200",
                ProductId = "Prod1",
                Date = DateTime.Now,
                PurchaserEmail = "test@example.com"
            };
            return order;
        }

        private static Mock<DurableOrchestrationContextBase> CreateMockDurableOrchestrationContext(Order order, string approvalResult, bool throwErrorInPdf = false)
        {
            var context = new Mock<DurableOrchestrationContextBase>();
            context.Setup(c => c.GetInput<Order>()).Returns(order);
            context.SetupGet(c => c.InstanceId).Returns("12345");
            if (throwErrorInPdf)
                context.Setup(c => c.CallActivityAsync<string>("A_CreatePersonalizedPdf", order)).ThrowsAsync(new InvalidOperationException("Failed to create PDF"));
            else
                context.Setup(c => c.CallActivityAsync<string>("A_CreatePersonalizedPdf", order)).ReturnsAsync("example.pdf");
            context.Setup(c => c.CallActivityAsync<string>("A_CreateWatermarkedVideo", order)).ReturnsAsync("example.mp4");
            context.Setup(c => c.WaitForExternalEvent<string>("OrderApprovalResult", It.IsAny<TimeSpan>(), null)).ReturnsAsync(approvalResult);
            return context;
        }
        private static bool CompareOrderResult(OrderResult expected, OrderResult actual) =>
            expected.Status == actual.Status && expected.Pdf == actual.Pdf && expected.Video == actual.Video;
    }
}
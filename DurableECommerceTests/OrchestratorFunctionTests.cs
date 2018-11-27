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
        public async Task ProcessOrder()
        {
            var order = CreateTestOrder();
            var context = new Mock<DurableOrchestrationContextBase>();
            context.Setup(c => c.GetInput<Order>()).Returns(order);
            context.SetupGet(c => c.InstanceId).Returns("12345");
            context.Setup(c => c.CallActivityAsync<string>("A_CreatePersonalizedPdf", order)).ReturnsAsync("example.pdf");
            context.Setup(c => c.CallActivityAsync<string>("A_CreateWatermarkedVideo", order)).ReturnsAsync("example.mp4");

            var orderResult = await OrchestratorFunctions.ProcessOrder(context.Object, mockLogger);

            context.Verify(c => c.CallActivityAsync("A_SaveOrderToDatabase", order), Times.Once);
            context.Verify(c => c.CallActivityAsync<string>("A_CreatePersonalizedPdf", order), Times.Once);
            context.Verify(c => c.CallActivityAsync<string>("A_CreateWatermarkedVideo", order), Times.Once);

            context.Verify(c => c.CallActivityWithRetryAsync("A_SendOrderConfirmationEmail",
                It.IsAny<RetryOptions>(),
                It.IsAny<object>()));

            Assert.That(orderResult.Status, Is.EqualTo("Success"));
            Assert.That(orderResult.Pdf, Is.EqualTo("example.pdf"));
            Assert.That(orderResult.Video, Is.EqualTo("example.mp4"));
        }

        private static Order CreateTestOrder()
        {
            var order = new Order
            {
                Amount = 50,
                Id = "102030",
                OrchestrationId = "100200",
                ProductId = "Prod1",
                Date = DateTime.Now,
                PurchaserEmail = "test@example.com"
            };
            return order;
        }
        // ActivityFunctions.CreatePersonalizedPdf - can't easily mock a CloudBlobContainer
        // ActivityFunctions.CreateWatermarkedVideo - can't easily mock a CloudBlobContainer

    }
}
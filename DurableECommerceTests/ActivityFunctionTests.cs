using System;
using System.Threading;
using System.Threading.Tasks;
using DurableECommerceWorkflow;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace DurableECommerceTests
{
    public class ActivityFunctionTests
    {
        private ILogger mockLogger;

        [SetUp]
        public void Setup()
        {
            mockLogger = Mock.Of<ILogger>();
        }

        [Test]
        public async Task CanSaveOrderToDatabase()
        {
            var order = CreateTestOrder();
            var collector = new Mock<IAsyncCollector<OrderEntity>>();
            OrderEntity entity = null;
            collector.Setup(c => c.AddAsync(It.IsAny<OrderEntity>(), It.IsAny<CancellationToken>()))
                .Callback((OrderEntity oe, CancellationToken ct) => entity = oe)
                .Returns(Task.CompletedTask);

            await ActivityFunctions.SaveOrderToDatabase(order, collector.Object, mockLogger);

            Assert.That(entity, Is.Not.Null);
            Assert.That(entity.OrchestrationId, Is.EqualTo(order.OrchestrationId));
            Assert.That(entity.ProductId, Is.EqualTo(order.ProductId));
            Assert.That(entity.OrderDate, Is.EqualTo(order.Date));
            Assert.That(entity.Amount, Is.EqualTo(order.Amount));
            Assert.That(entity.Email, Is.EqualTo(order.PurchaserEmail));
            Assert.That(entity.RowKey, Is.EqualTo(order.Id));
            Assert.That(entity.PartitionKey, Is.EqualTo(OrderEntity.OrderPartitionKey));
        }

        private static Order CreateTestOrder()
        {
            var order = new Order
            {
                Amount = 1234,
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
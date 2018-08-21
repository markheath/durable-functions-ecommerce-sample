using System;

namespace DurableECommerceWorkflow
{
    public class Order
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ProductId { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public decimal Amount { get; set; }
        public string PurchaserEmail { get; set; }

        public string OrchestrationId { get; set; }
    }
}

using System;

namespace DurableECommerceWorkflow
{
    public class Order
    {
        public string ProductId { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public decimal Amount { get; set; }
        public string PurchaserEmail { get; set; }
    }
}

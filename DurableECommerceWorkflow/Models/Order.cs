using System;
using System.Linq;

namespace DurableECommerceWorkflow.Models;

public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Date { get; set; } = DateTime.Now;
    public string PurchaserEmail { get; set; }
    public OrderItem[] Items { get; set; }
    public string OrchestrationId { get; set; }
}

public static class OrderExtensions
{
    public static int ItemCount(this Order order)
    {
        return order.Items.Length;
    }

    public static decimal Total(this Order order)
    {
        return order.Items.Sum(i => i.Amount);
    }
}

public class OrderItem
{
    public string ProductId { get; set; }
    public decimal Amount { get; set; }

}


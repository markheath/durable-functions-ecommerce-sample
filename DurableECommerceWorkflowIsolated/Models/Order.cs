namespace DurableECommerceWorkflowIsolated.Models;

public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Date { get; set; } = DateTime.Now;
    public string PurchaserEmail { get; set; } = String.Empty;
    public OrderItem[] Items { get; set; } = Array.Empty<OrderItem>();
    public string OrchestrationId { get; set; } = String.Empty;
}

public record PdfInfo(string OrderId, string ProductId, string PurchaserEmail);
public record ConfirmationInfo(Order Order, string[] Files);

public static class OrderExtensions
{
    public static int ItemCount(this Order order)
    {
        return order.Items?.Length ?? 0;
    }

    public static decimal Total(this Order order)
    {
        return order.Items?.Sum(i => i.Amount) ?? 0;
    }
}

public class OrderItem
{
    public string? ProductId { get; set; }
    public decimal Amount { get; set; }

}


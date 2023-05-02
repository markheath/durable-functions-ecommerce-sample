using Azure;
using Azure.Data.Tables;

namespace DurableECommerceWorkflowIsolated.Models;

// for the purposes of storing in table storage
public class OrderEntity : ITableEntity
{
    public const string TableName = "Orders";
    public const string OrderPartitionKey = "ORDER";

    public string PartitionKey { get; set; } = OrderPartitionKey;
    public string? RowKey { get; set; }
    public string OrchestrationId { get; set; } = String.Empty;
    public string? Items { get; set; }
    public string? Email { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

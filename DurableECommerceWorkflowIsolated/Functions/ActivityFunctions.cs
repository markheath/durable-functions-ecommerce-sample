using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using DurableECommerceWorkflowIsolated.Extensions;
using DurableECommerceWorkflowIsolated.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace DurableECommerceWorkflowIsolated.Functions;

public class ActivityFunctions
{
    private readonly TableServiceClient tableServiceClient;
    private readonly BlobServiceClient blobServiceClient;
    private readonly ISendGridClient sendGridClient;

    // doesn't seem like we can mix activity fnctions with table output at the moment? or some other issue
    // https://github.com/Azure/azure-functions-dotnet-worker/blob/main/samples/Extensions/Table/TableFunction.cs
    // [TableOutput(OrderEntity.TableName, Connection = "AzureWebJobsStorage")]

    public ActivityFunctions(TableServiceClient tableServiceClient,
        BlobServiceClient blobServiceClient,
        ISendGridClient sendGridClient)
    {
        this.tableServiceClient = tableServiceClient;
        this.blobServiceClient = blobServiceClient;
        this.sendGridClient = sendGridClient;
    }

    [Function("A_SaveOrderToDatabase")]
    public async Task SaveOrderToDatabase(
                        [ActivityTrigger] Order order,
                        FunctionContext context)
    {
        var log = context.GetLogger(nameof(SaveOrderToDatabase));
        // can't inject as function parameter
        log.LogInformation($"Saving order {order.Id} to database");
        var tableClient = tableServiceClient.GetTableClient(OrderEntity.TableName);
        await tableClient.CreateIfNotExistsAsync();

        var orderEntity = new OrderEntity
        {
            PartitionKey = OrderEntity.OrderPartitionKey,
            RowKey = order.Id,
            OrchestrationId = order.OrchestrationId,
            Items = string.Join(",", order.Items.Select(i => i.ProductId)),
            Email = order.PurchaserEmail,
            OrderDate = DateTime.UtcNow.Date,
            Amount = order.Items.Sum(i => i.Amount)
        };
        var resp = await tableClient.AddEntityAsync(orderEntity);
        if (resp.IsError)
        {
            log.LogError($"Failed to write order {order.Id} for orchestration {order.OrchestrationId} to table storage");
        }
    }

    [Function("A_CreatePersonalizedPdf")]
    public async Task<string> CreatePersonalizedPdf(
                        [ActivityTrigger] PdfInfo pdfInfo,
                         FunctionContext context)
    {
        var log = context.GetLogger(nameof(CreatePersonalizedPdf));
        var assetsContainer = blobServiceClient.GetBlobContainerClient("assets");

        log.LogInformation("Creating PDF");
        if (pdfInfo.ProductId == "error")
            throw new InvalidOperationException("Can't create the PDF for this product");
        var fileName = $"{pdfInfo.OrderId}/{pdfInfo.ProductId}-pdf.txt";
        await assetsContainer.CreateIfNotExistsAsync();
        var blob = assetsContainer.GetBlobClient(fileName);
        await blob.UploadTextAsync($"Example {pdfInfo.ProductId} PDF for {pdfInfo.PurchaserEmail}");
        return blob.GenerateSasUri(BlobSasPermissions.Read,
                DateTimeOffset.UtcNow.AddDays(1)).ToString();
    }

    [Function("A_SendOrderConfirmationEmail")]
    public async Task SendOrderConfirmationEmail(
                [ActivityTrigger] ConfirmationInfo input,
                FunctionContext context)
    {
        var log = context.GetLogger(nameof(SendOrderConfirmationEmail));

        log.LogInformation($"Sending Order Confirmation Email to {input.Order.PurchaserEmail}");
        var body = $"Thanks for your order, you can download your files here: " +
            string.Join(" ", input.Order.Items?.Zip(input.Files, (i, f) => $"<a href=\"{f}\">{i.ProductId}</a><br/>") ?? Array.Empty<string>());
        var message = GenerateMail(input.Order.PurchaserEmail, $"Your order {input.Order.Id}", body);
        await sendGridClient.PostAsync(message, log);
    }

    [Function("A_SendProblemEmail")]
    public async Task SendProblemEmail(
                [ActivityTrigger] Order order,
                FunctionContext context)
    {
        var log = context.GetLogger(nameof(SendProblemEmail));

        log.LogInformation($"Sending Problem Email {order.PurchaserEmail}");
        var body = "We're very sorry there was a problem processing your order. <br/>" +
            " Please contact customer support.";
        var message = GenerateMail(order.PurchaserEmail, $"Problem with order {order.Id}", body);
        await sendGridClient.PostAsync(message, log);
    }

    private static SendGridMessage GenerateMail(string recipient, string subject, string body)
    {
        var recipientEmail = new EmailAddress(recipient);
        var senderEmail = new EmailAddress(Environment.GetEnvironmentVariable("SenderEmail"));

        var message = new SendGridMessage();
        message.Subject = subject;
        message.From = senderEmail;
        message.AddTo(recipientEmail);
        message.HtmlContent = body;
        return message;
    }

    [Function("A_SendNotApprovedEmail")]
    public async Task SendNotApprovedEmail(
        [ActivityTrigger] Order order,
        FunctionContext context)
    {
        var log = context.GetLogger(nameof(SendNotApprovedEmail));

        log.LogInformation($"Sending Not Approved Email {order.PurchaserEmail}");
        var body = $"We're very sorry we were not able to approve your order #{order.Id}. <br/>" +
            " Please contact customer support.";
        var message = GenerateMail(order.PurchaserEmail, $"Order {order.Id} rejected", body);
        await sendGridClient.PostAsync(message, log);
    }

    [Function("A_RequestOrderApproval")]
    public async Task RequestOrderApproval(
        [ActivityTrigger] Order order,
        FunctionContext context)
    {
        var log = context.GetLogger(nameof(RequestOrderApproval));

        log.LogInformation($"Requesting Approval for Order {order.PurchaserEmail}");
        var subject = $"Order {order.Id} requires approval";
        var approverEmail = Environment.GetEnvironmentVariable("ApproverEmail");
        var host = Environment.GetEnvironmentVariable("Host");

        var approveUrl = $"{host}/manage";
        var body = $"Please review <a href=\"{approveUrl}\">Order {order.Id}</a><br>"
                           + $"for product {string.Join(",", order.Items.Select(o => o.ProductId))}"
                           + $" and amount ${order.Total()}";

        var message = GenerateMail(approverEmail, subject, body);
        await sendGridClient.PostAsync(message, log);
    }
}

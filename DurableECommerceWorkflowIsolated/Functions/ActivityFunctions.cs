using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using DurableECommerceWorkflowIsolated.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace DurableECommerceWorkflowIsolated.Functions;

public static class ActivityFunctions
{
    // doesn't seem like we can mix activity fnctions with table output at the moment? or some other issue
    // https://github.com/Azure/azure-functions-dotnet-worker/blob/main/samples/Extensions/Table/TableFunction.cs
    // [TableOutput(OrderEntity.TableName, Connection = "AzureWebJobsStorage")]

    [Function("A_SaveOrderToDatabase")]
    public static void SaveOrderToDatabase(
                        [ActivityTrigger] Order order,
                        FunctionContext context)
    {
        var log = context.GetLogger(nameof(SaveOrderToDatabase));
        // can't inject as function parameter
        var tableServiceClient = context.InstanceServices.GetRequiredService<TableServiceClient>();
        log.LogInformation($"Saving order {order.Id} to database");
        var tableClient = tableServiceClient.GetTableClient(OrderEntity.TableName);
        
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
        var resp = tableClient.AddEntity(orderEntity);
        if (resp.IsError)
        {
            log.LogError($"Failed to write order {order.Id} for orchestration {order.OrchestrationId} to table storage");
        }
    }

    [Function("A_CreatePersonalizedPdf")]
    public static async Task<string> CreatePersonalizedPdf(
                        [ActivityTrigger] PdfInfo pdfInfo,
                         FunctionContext context)
    {
        var log = context.GetLogger(nameof(CreatePersonalizedPdf));
        var blobServiceClient = context.InstanceServices.GetRequiredService<BlobServiceClient>();
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
    public static async Task SendOrderConfirmationEmail(
                [ActivityTrigger] ConfirmationInfo input,
                FunctionContext context)
    {
        var log = context.GetLogger(nameof(SendOrderConfirmationEmail));
        var sender = context.InstanceServices.GetRequiredService<ISendGridClient>();

        log.LogInformation($"Sending Order Confirmation Email to {input.Order.PurchaserEmail}");
        var body = $"Thanks for your order, you can download your files here: " +
            string.Join(" ", input.Order.Items?.Zip(input.Files, (i, f) => $"<a href=\"{f}\">{i.ProductId}</a><br/>") ?? Array.Empty<string>());
        var message = GenerateMail(input.Order.PurchaserEmail, $"Your order {input.Order.Id}", body);
        await sender.PostAsync(message, log);
    }

    [Function("A_SendProblemEmail")]
    public static async Task SendProblemEmail(
                [ActivityTrigger] Order order,
                FunctionContext context)
    {
        var log = context.GetLogger(nameof(SendProblemEmail));
        var sender = context.InstanceServices.GetRequiredService<ISendGridClient>();

        log.LogInformation($"Sending Problem Email {order.PurchaserEmail}");
        var body = "We're very sorry there was a problem processing your order. <br/>" +
            " Please contact customer support.";
        var message = GenerateMail(order.PurchaserEmail, $"Problem with order {order.Id}", body);
        await sender.PostAsync(message, log);
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
    public static async Task SendNotApprovedEmail(
        [ActivityTrigger] Order order,
        FunctionContext context)
    {
        var log = context.GetLogger(nameof(SendNotApprovedEmail));
        var sender = context.InstanceServices.GetRequiredService<ISendGridClient>();

        log.LogInformation($"Sending Not Approved Email {order.PurchaserEmail}");
        var body = $"We're very sorry we were not able to approve your order #{order.Id}. <br/>" +
            " Please contact customer support.";
        var message = GenerateMail(order.PurchaserEmail, $"Order {order.Id} rejected", body);
        await sender.PostAsync(message, log);
    }

    [Function("A_RequestOrderApproval")]
    public static async Task RequestOrderApproval(
        [ActivityTrigger] Order order,
        FunctionContext context)
    {
        var log = context.GetLogger(nameof(RequestOrderApproval));
        var sender = context.InstanceServices.GetRequiredService<ISendGridClient>();

        log.LogInformation($"Requesting Approval for Order {order.PurchaserEmail}");
        var subject = $"Order {order.Id} requires approval";
        var approverEmail = Environment.GetEnvironmentVariable("ApproverEmail");
        var host = Environment.GetEnvironmentVariable("Host");

        var approveUrl = $"{host}/manage";
        var body = $"Please review <a href=\"{approveUrl}\">Order {order.Id}</a><br>"
                           + $"for product {string.Join(",", order.Items.Select(o => o.ProductId))}"
                           + $" and amount ${order.Total()}";

        var message = GenerateMail(approverEmail, subject, body);
        await sender.PostAsync(message, log);
    }

    private static async Task PostAsync(this ISendGridClient sender, SendGridMessage message, ILogger log)
    {
        var sendGridKey = Environment.GetEnvironmentVariable("SendGridKey");
        // don't actually try to send SendGrid emails if we are just using example or missing email addresses
        var testMode = string.IsNullOrEmpty(sendGridKey) || sendGridKey == "TEST" || message.Personalizations.SelectMany(p => p.Tos.Select(t => t.Email))
            .Any(e => string.IsNullOrEmpty(e) || e.Contains("@example") || e.Contains("@email"));
        if (testMode)
        {
            log.LogWarning($"Sending email with body {message.HtmlContent}");
        }
        else
        {
            await sender.SendEmailAsync(message);
        }
    }
}

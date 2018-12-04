using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;
using SendGrid.Helpers.Mail;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DurableECommerceWorkflow
{
    public static class ActivityFunctions
    {

        [FunctionName("A_SaveOrderToDatabase")]
        public static async Task SaveOrderToDatabase(
                            [ActivityTrigger] Order order,
                            [Table(OrderEntity.TableName)] IAsyncCollector<OrderEntity> table,
                            ILogger log)
        {
            log.LogInformation("Saving order to database");
            await table.AddAsync(new OrderEntity
            {
                PartitionKey = OrderEntity.OrderPartitionKey,
                RowKey = order.Id,
                OrchestrationId = order.OrchestrationId,
                Items = string.Join(",",order.Items.Select(i => i.ProductId)),
                Email = order.PurchaserEmail,
                OrderDate = order.Date,
                Amount = order.Items.Sum(i => i.Amount)
            });
        }

        [FunctionName("A_CreatePersonalizedPdf")]
        public static async Task<string> CreatePersonalizedPdf(
                            [ActivityTrigger] (Order, OrderItem) orderInfo,
                            [Blob("assets")] CloudBlobContainer assets,
                            ILogger log)
        {
            var (order, item) = orderInfo;
            log.LogInformation("Creating PDF");
            if (item.ProductId == "error")
                throw new InvalidOperationException("Can't create the PDF for this product");
            var fileName = $"{order.Id}/{item.ProductId}-pdf.txt";
            await assets.CreateIfNotExistsAsync();
            var blob = assets.GetBlockBlobReference(fileName);
            await blob.UploadTextAsync($"Example {item.ProductId} PDF for {order.PurchaserEmail}");
            return GetSasUri(blob);
        }

        private static string GetSasUri(CloudBlockBlob blob)
        {
            var sas = blob.GetSharedAccessSignature(new SharedAccessBlobPolicy()
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessStartTime = DateTimeOffset.UtcNow,
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddDays(1),
            });
            return blob.StorageUri.PrimaryUri + sas;
        }

        [FunctionName("A_SendOrderConfirmationEmail")]
        public static async Task SendOrderConfirmationEmail(
                    [ActivityTrigger] (Order, string[]) input,
                    [SendGrid(ApiKey = "SendGridKey")] IAsyncCollector<SendGridMessage> sender,
                    ILogger log)
        {
            var (order, files) = input;
            log.LogInformation($"Sending Order Confirmation Email to {order.PurchaserEmail}");
            var body = $"Thanks for your order, you can download your files here: " +
                string.Join(" ",order.Items.Zip(files, (i,f) => $"<a href=\"{f}\">{i.ProductId}</a><br/>"));
            var message = GenerateMail(order.PurchaserEmail, $"Your order {order.Id}", body);
            await sender.PostAsync(message,log);
        }

        [FunctionName("A_SendProblemEmail")]
        public static async Task SendProblemEmail(
                    [ActivityTrigger] Order order,
                    [SendGrid(ApiKey = "SendGridKey")] IAsyncCollector<SendGridMessage> sender,
                    ILogger log)
        {
            log.LogInformation($"Sending Problem Email {order.PurchaserEmail}");
            var body = "We're very sorry there was a problem processing your order. <br/>" +
                " Please contact customer support.";
            var message = GenerateMail(order.PurchaserEmail, $"Problem with order {order.Id}", body);
            await sender.PostAsync(message,log);
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

        [FunctionName("A_SendNotApprovedEmail")]
        public static async Task SendNotApprovedEmail(
            [ActivityTrigger] Order order,
            [SendGrid(ApiKey = "SendGridKey")] IAsyncCollector<SendGridMessage> sender,
            ILogger log)
        {
            log.LogInformation($"Sending Not Approved Email {order.PurchaserEmail}");
            var body = $"We're very sorry we were not able to approve your order #{order.Id}. <br/>" +
                " Please contact customer support.";
            var message = GenerateMail(order.PurchaserEmail, $"Order {order.Id} rejected", body);
            await sender.PostAsync(message,log);
        }

        [FunctionName("A_RequestOrderApproval")]
        public static async Task RequestOrderApproval(
            [ActivityTrigger] Order order,
            [SendGrid(ApiKey = "SendGridKey")] IAsyncCollector<SendGridMessage> sender,
            ILogger log)
        {
            log.LogInformation($"Requesting Approval for Order {order.PurchaserEmail}");
            var subject = $"Order {order.Id} requires approval";
            var approverEmail = Environment.GetEnvironmentVariable("ApproverEmail");
            var host = Environment.GetEnvironmentVariable("Host");

            var approveUrl = $"{host}/manage";
            var body = $"Please review <a href=\"{approveUrl}\">Order {order.Id}</a><br>"
                               + $"for product {string.Join(",",order.Items.Select(o=>o.ProductId))}"
                               + $" and amount ${order.Total()}";

            var message = GenerateMail(approverEmail, subject, body);
            await sender.PostAsync(message, log);
        }

        private static async Task PostAsync(this IAsyncCollector<SendGridMessage> sender, SendGridMessage message, ILogger log)
        {
            // don't actually try to send SendGrid emails if we are just using example or missing email addresses
            var testMode = message.Personalizations.SelectMany(p => p.Tos.Select(t => t.Email))
                .Any(e => String.IsNullOrEmpty(e) || e.Contains("@example") || e.Contains("@email"));
            if (testMode)
            {
                log.LogWarning($"Sending email with body {message.HtmlContent}");
            }
            else
            {
                await sender.AddAsync(message);
            }

        }
    }
}

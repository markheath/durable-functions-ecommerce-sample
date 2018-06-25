using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using SendGrid.Helpers.Mail;
using System;
using System.Threading.Tasks;

namespace DurableECommerceWorkflow
{
    public static class ActivityFunctions
    {

        [FunctionName("A_SaveOrderToDatabase")]
        public static async Task SaveOrderToDatabase(
                            [ActivityTrigger] Order order,
                            [Table("Orders")] IAsyncCollector<OrderEntity> table,
                            TraceWriter log)
        {
            log.Info("Saving order to database");
            await table.AddAsync(new OrderEntity
            {
                PartitionKey = order.ProductId,
                RowKey = order.Id,
                Email = order.PurchaserEmail,
                OrderDate = order.Date,
                Amount = order.Amount
            });
        }

        [FunctionName("A_CreatePersonalizedPdf")]
        public static async Task<string> CreatePersonalizedPdf(
                            [ActivityTrigger] Order order,
                            [Blob("assets")] CloudBlobContainer assets,
                            TraceWriter log)
        {
            log.Info("Creating PDF");
            if (order.ProductId == "error")
                throw new InvalidOperationException("Can't create the PDF for this product");
            var fileName = $"{order.Id}/{order.ProductId}.pdf";
            await assets.CreateIfNotExistsAsync();
            var blob = assets.GetBlockBlobReference(fileName);
            await blob.UploadTextAsync($"Example {order.ProductId} PDF for {order.PurchaserEmail}");
            return GetSasUri(blob);
        }

        [FunctionName("A_CreateWatermarkedVideo")]
        public static async Task<string> CreateWatermarkedVideo(
            [ActivityTrigger] Order order,
            [Blob("assets")] CloudBlobContainer assets,
            TraceWriter log)
        {
            log.Info("Creating Watermarked Video");
            var fileName = $"{order.Id}/{order.ProductId}.mp4";
            await assets.CreateIfNotExistsAsync();
            var blob = assets.GetBlockBlobReference(fileName);
            await blob.UploadTextAsync($"Example {order.ProductId} Video for {order.PurchaserEmail}");
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

        [FunctionName("A_SendEmail")]
        public static void SendEmail(
                    [ActivityTrigger] (Order, string, string) input,
                    [SendGrid(ApiKey = "SendGridKey")] out SendGridMessage message,
                    TraceWriter log)
        {
            var (order, pdfLoc, videoLoc) = input;
            log.Info($"Sending Email to {order.PurchaserEmail}");
            var body = $"Thanks for your order, you can download your files here: " +
                $"<a href=\"{pdfLoc}\">PDF</a> <a href=\"{videoLoc}\">Video</a>";
            log.Warning(body);
            message = GenerateMail(order.PurchaserEmail, $"Your order {order.Id}", body);
        }

        [FunctionName("A_SendProblemEmail")]
        public static void SendProblemEmail(
                    [ActivityTrigger] Order order,
                    [SendGrid(ApiKey = "SendGridKey")] out SendGridMessage message,
                    TraceWriter log)
        {
            log.Info($"Sending Problem Email {order.PurchaserEmail}");
            var body = "We're very sorry there was a problem processing your order. <br/>" +
                " Please contact customer support";
            log.Warning(body);
            message = GenerateMail(order.PurchaserEmail, $"Your order {order.Id}", body);
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
        public static void SendNotApprovedEmail(
            [ActivityTrigger] Order order,
            [SendGrid(ApiKey = "SendGridKey")] out SendGridMessage message,
            TraceWriter log)
        {
            log.Info($"Sending Not Approved Email {order.PurchaserEmail}");
            var body = "We're very sorry we were not able to approve your order. <br/>" +
                " Please contact customer support";
            log.Warning(body);
            message = GenerateMail(order.PurchaserEmail, $"Your order {order.Id}", body);
        }

        [FunctionName("A_RequestOrderApproval")]
        public static void RequestOrderApproval(
            [ActivityTrigger] Order order,
            [SendGrid(ApiKey = "SendGridKey")] out SendGridMessage message,
            TraceWriter log)
        {
            log.Info($"Requesting Approval for Order {order.PurchaserEmail}");
            var subject = $"Order {order.Id} requires approval";
            var approverEmail = Environment.GetEnvironmentVariable("ApproverEmail");

            var body = $"Please review {order.Id}<br>"
                               + $"for product {order.ProductId}"
                               + $"and amount {order.Amount}";

            message = GenerateMail(order.PurchaserEmail, subject, body);
        }
    }
}

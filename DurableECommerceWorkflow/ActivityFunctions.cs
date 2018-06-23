using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System;

namespace DurableECommerceWorkflow
{
    public static class ActivityFunctions
    {
        [FunctionName("A_SaveOrderToDatabase")]
        public static void SaveOrderToDatabase(
                            [ActivityTrigger] Order order,
                            TraceWriter log)
        {
            log.Info("Saving order to database");
        }

        [FunctionName("A_CreatePersonalizedPdf")]
        public static string CreatePersonalizedPdf(
                            [ActivityTrigger] Order order,
                            TraceWriter log)
        {
            log.Info("Creating PDF");
            if (order.ProductId == "error")
                throw new InvalidOperationException("Can't create the PDF for this product");
            return $"{order.Id}.pdf";
        }

        [FunctionName("A_SendEmail")]
        public static void SendEmail(
                    [ActivityTrigger] (Order, string, string) input,
                    TraceWriter log)
        {
            var (order, pdfLoc, videoLoc) = input;
            log.Info($"Sending Email to {order.PurchaserEmail}");
            log.Warning("Thanks for your order, you can download your files here:");
            log.Warning(pdfLoc);
            log.Warning(videoLoc);
        }

        [FunctionName("A_SendProblemEmail")]
        public static void SendProblemEmail(
                    [ActivityTrigger] Order order,
                    TraceWriter log)
        {
            log.Info($"Sending Problem Email {order.PurchaserEmail}");
            log.Warning("We're very sorry there was a problem processing your order");
            log.Warning("Please contact customer support");
        }

        [FunctionName("A_CreateWatermarkedVideo")]
        public static string CreateWatermarkedVideo(
                    [ActivityTrigger] Order order,
                    TraceWriter log)
        {
            log.Info("Creating Watermarked Video");
            return $"{order.Id}.mp4";
        }
    }
}

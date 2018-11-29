using Microsoft.Azure.WebJobs;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DurableECommerceWorkflow
{
    public static class OrchestratorFunctions
    {
        [FunctionName("O_ProcessOrder")]
        public static async Task<OrderResult> ProcessOrder(
            [OrchestrationTrigger] DurableOrchestrationContextBase ctx,
            ILogger log)
        {
            var order = ctx.GetInput<Order>();
            order.OrchestrationId = ctx.InstanceId;

            if (!ctx.IsReplaying)
                log.LogInformation($"Processing order for {order.ProductId}");

            await ctx.CallActivityAsync("A_SaveOrderToDatabase", order);

            if (order.Amount > 1000)
            {
                if (!ctx.IsReplaying)
                    log.LogWarning($"Need approval for {ctx.InstanceId}");

                ctx.SetCustomStatus("Needs approval");
                await ctx.CallActivityAsync("A_RequestOrderApproval", order);

                var approvalResult = await ctx.WaitForExternalEvent<string>("OrderApprovalResult", TimeSpan.FromSeconds(180), null);
                ctx.SetCustomStatus(""); // clear the needs approval flag

                if (approvalResult != "Approved")
                {
                    // timed out or got a rejected
                    if (!ctx.IsReplaying)
                        log.LogWarning($"Not approved [{approvalResult}]");
                    await ctx.CallActivityAsync("A_SendNotApprovedEmail", order);
                    return new OrderResult { Status = "NotApproved" }; 
                }

            }

            string pdfLocation = null;
            string videoLocation = null;
            try
            {
                // create files in parallel
                var pdfTask = ctx.CallActivityAsync<string>("A_CreatePersonalizedPdf", order);
                var videoTask = ctx.CallActivityAsync<string>("A_CreateWatermarkedVideo", order);
                await Task.WhenAll(pdfTask, videoTask);
                pdfLocation = pdfTask.Result;
                videoLocation = videoTask.Result;
            }
            catch (Exception ex)
            {
                if (!ctx.IsReplaying)
                    log.LogError($"Failed to create files", ex);
            }

            if (pdfLocation != null && videoLocation != null)
            {
                await ctx.CallActivityWithRetryAsync("A_SendOrderConfirmationEmail",
                    new RetryOptions(TimeSpan.FromSeconds(30), 3),
                    (order, pdfLocation, videoLocation));
                return new OrderResult { Status = "Success", Pdf = pdfLocation, Video = videoLocation };
            }
            await ctx.CallActivityWithRetryAsync("A_SendProblemEmail",
                new RetryOptions(TimeSpan.FromSeconds(30), 3),
                order);
            return new OrderResult { Status = "Problem" };
        }

    }
}

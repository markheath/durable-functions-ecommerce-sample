using Azure.Identity;
using DurableECommerceWorkflowIsolated.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace DurableECommerceWorkflowIsolated.Functions;

public static class OrchestratorFunctions
{
    [Function("O_ProcessOrder")]
    public static async Task<OrderResult> ProcessOrder(
        [OrchestrationTrigger] TaskOrchestrationContext ctx,
        FunctionContext functionContext)
    {
        var log = ctx.CreateReplaySafeLogger(functionContext.GetLogger(nameof(ProcessOrder)));
        
        var order = ctx.GetInput<Order>();
        order.OrchestrationId = ctx.InstanceId;

        if (!ctx.IsReplaying)
            log.LogInformation($"Processing order #{order.Id}");

        await ctx.CallActivityAsync("A_SaveOrderToDatabase", order);

        var total = order.Items.Sum(i => i.Amount);
        if (total > 1000)
        {
            log.LogWarning($"Need approval for {ctx.InstanceId}");

            ctx.SetCustomStatus("Needs approval");
            await ctx.CallActivityAsync("A_RequestOrderApproval", order);

            string approvalResult;
            try
            {
                approvalResult = await ctx.WaitForExternalEvent<string>("OrderApprovalResult", TimeSpan.FromSeconds(180));
            }
            catch (TaskCanceledException) // not a TimeoutException as you might expect
            {
                log.LogWarning($"Timed out waiting for approval");
                approvalResult = "Timed out";
            }
            catch(Exception e)
            {
                log.LogError(e, $"Something else");
                approvalResult = "Timed out 2";

            }

            ctx.SetCustomStatus(""); // clear the needs approval flag
            if (approvalResult != "Approved")
            {
                // timed out or got a rejected
                log.LogWarning($"Not approved [{approvalResult}]");
                await ctx.CallActivityAsync("A_SendNotApprovedEmail", order);
                return new OrderResult { Status = "NotApproved" };
            }

        }

        string[]? downloads = null;
        try
        {
            // create files in parallel
            var tasks = new List<Task<string>>();
            foreach (var item in order.Items)
            {
                tasks.Add(ctx.CallActivityAsync<string>("A_CreatePersonalizedPdf", new PdfInfo(order.Id,item.ProductId,order.PurchaserEmail)));
            }

            downloads = await Task.WhenAll(tasks);

        }
        catch (Exception ex)
        {
            if (!ctx.IsReplaying)
                log.LogError($"Failed to create files", ex);
        }

        if (downloads != null)
        {
            await ctx.CallActivityAsync("A_SendOrderConfirmationEmail",
                
                new ConfirmationInfo(order, downloads),
                TaskOptions.FromRetryPolicy(new RetryPolicy(3, TimeSpan.FromSeconds(30))));
            return new OrderResult { Status = "Success", Downloads = downloads };
        }
        await ctx.CallActivityAsync("A_SendProblemEmail",
            order,
            TaskOptions.FromRetryPolicy(new RetryPolicy(3, TimeSpan.FromSeconds(30))));
        return new OrderResult { Status = "Problem" };
    }

}

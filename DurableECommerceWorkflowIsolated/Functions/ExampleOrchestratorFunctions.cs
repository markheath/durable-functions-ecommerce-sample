using DurableECommerceWorkflowIsolated.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace DurableECommerceWorkflowIsolated.Functions;

static class ExampleOrchestratorFunctions
{

    [Function("O_ProcessOrder_V3")]
    public static async Task<string> ProcessOrderV3(
        [OrchestrationTrigger] TaskOrchestrationContext ctx,
        FunctionContext functionContext)
    {
        var log = functionContext.GetLogger(nameof(ProcessOrderV3));
        var order = ctx.GetInput<Order>();

        if (!ctx.IsReplaying)
            log.LogInformation($"Processing order {order.Id}");

        await ctx.CallActivityAsync("A_SaveOrderToDatabase", order);

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
            await ctx.CallActivityAsync("A_SendOrderConfirmationEmail",
                (order, pdfLocation, videoLocation),
                new TaskOptions(new TaskRetryOptions(new RetryPolicy(3, TimeSpan.FromSeconds(30)))));
            return "Order processed successfully";
        }
        await ctx.CallActivityAsync("A_SendProblemEmail",
            order,
            TaskOptions.FromRetryPolicy(new RetryPolicy(3, TimeSpan.FromSeconds(30))));
        return "There was a problem processing this order";
    }


    [Function("O_ProcessOrder_V2")]
    public static async Task<string> ProcessOrderV2(
        [OrchestrationTrigger] TaskOrchestrationContext ctx,
        FunctionContext functionContext)
    {
        var log = functionContext.GetLogger(nameof(ProcessOrderV2));
        var order = ctx.GetInput<Order>();
        if (order == null) throw new InvalidOperationException("failed to deserialize orchestration input");

        if (!ctx.IsReplaying)
            log.LogInformation($"Processing order {order.Id}");

        await ctx.CallActivityAsync("A_SaveOrderToDatabase", order);

        // create files in parallel
        var pdfTask = ctx.CallActivityAsync<string>("A_CreatePersonalizedPdf", order);
        var videoTask = ctx.CallActivityAsync<string>("A_CreateWatermarkedVideo", order);
        await Task.WhenAll(pdfTask, videoTask);

        var pdfLocation = pdfTask.Result;
        var videoLocation = videoTask.Result;

        await ctx.CallActivityAsync("A_SendOrderConfirmationEmail", (order, pdfLocation, videoLocation));

        return "Order processed successfully";
    }

    // basic orchestrator, calls all functions in sequence
    [Function("O_ProcessOrder_V1")]
    public static async Task<string> ProcessOrderV1(
        [OrchestrationTrigger] TaskOrchestrationContext ctx,
        FunctionContext functionContext)
    {
        var log = functionContext.GetLogger(nameof(ProcessOrderV1));
        var order = ctx.GetInput<Order>();
        if (order == null) throw new InvalidOperationException("failed to deserialize orchestration input");

        if (!ctx.IsReplaying)
            log.LogInformation($"Processing order {order.Id}");

        await ctx.CallActivityAsync("A_SaveOrderToDatabase", order);
        var pdfLocation = await ctx.CallActivityAsync<string>("A_CreatePersonalizedPdf", order);
        var videoLocation = await ctx.CallActivityAsync<string>("A_CreateWatermarkedVideo", order);
        await ctx.CallActivityAsync("A_SendOrderConfirmationEmail", (order, pdfLocation, videoLocation));

        return "Order processed successfully";
    }
}

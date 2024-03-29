using System.IO;
using System.Threading.Tasks;
using DurableECommerceWorkflow.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DurableECommerceWorkflow.Functions;

public static class ApproveOrderFunctions
{
    [FunctionName(nameof(ApproveOrderById))]
    public static async Task<IActionResult> ApproveOrderById(
        [HttpTrigger(AuthorizationLevel.Anonymous,
            "post", Route = "approve/{id}")]HttpRequest req,
        [DurableClient] IDurableOrchestrationClient client,
        [Table(OrderEntity.TableName, OrderEntity.OrderPartitionKey, "{id}", Connection = "AzureWebJobsStorage")] OrderEntity order,
        ILogger log, string id)
    {
        log.LogInformation($"Setting approval status of order {id}");

        if (order == null)
        {
            return new NotFoundResult();
        }

        var body = await req.ReadAsStringAsync(); // should be "Approved" or "Rejected"
        var status = JsonConvert.DeserializeObject<string>(body);
        await client.RaiseEventAsync(order.OrchestrationId, "OrderApprovalResult", status);

        return new OkResult();
    }

    [FunctionName(nameof(ApproveOrder))]
    public static async Task<IActionResult> ApproveOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous,
            "post", Route = null)]HttpRequest req,
        [DurableClient] IDurableOrchestrationClient client,
        ILogger log)
    {
        log.LogInformation("Received an approval result.");
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var approvalResult = JsonConvert.DeserializeObject<ApprovalResult>(requestBody);
        await client.RaiseEventAsync(approvalResult.OrchestrationId, "OrderApprovalResult", approvalResult.Approved ? "Approved" : "Rejected");
        log.LogInformation($"Approval Result for {approvalResult.OrchestrationId} is {approvalResult.Approved}");
        return new OkResult();
    }
}

using System.Net;
using Azure.Data.Tables;
using DurableECommerceWorkflowIsolated.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DurableECommerceWorkflowIsolated.ApiFunctions;

public static class ApproveOrderFunctions
{
    [Function(nameof(ApproveOrderById))]
    public static async Task<HttpResponseData> ApproveOrderById(
        [HttpTrigger(AuthorizationLevel.Anonymous,
            "post", Route = "approve/{id}")]HttpRequestData req,
        [DurableClient] DurableClientContext clientContext,
        //[TableInput(OrderEntity.TableName, OrderEntity.OrderPartitionKey, "{id}", Connection = "AzureWebJobsStorage")] OrderEntity order,
        FunctionContext functionContext, string id)
    {
        var log = functionContext.GetLogger(nameof(ApproveOrderById));
        log.LogInformation($"Setting approval status of order {id}");


        var tableServiceClient = functionContext.InstanceServices.GetRequiredService<TableServiceClient>();
        var tableClient = tableServiceClient.GetTableClient(OrderEntity.TableName);
        var orderResp = await tableClient.GetEntityIfExistsAsync<OrderEntity>(OrderEntity.OrderPartitionKey, id);

        if (!orderResp.HasValue)
        {
            log.LogWarning($"Cannot find order {id}");
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
        var order = orderResp.Value;


        var body = await req.ReadAsStringAsync(); // should be "Approved" or "Rejected"
        var status = JsonConvert.DeserializeObject<string>(body);
        await clientContext.Client.RaiseEventAsync(order.OrchestrationId, "OrderApprovalResult", status);

        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function(nameof(ApproveOrder))]
    public static async Task<HttpResponseData> ApproveOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous,
            "post", Route = null)]HttpRequestData req,
        [DurableClient] DurableClientContext client,
        FunctionContext functionContext)
    {
        var log = functionContext.GetLogger(nameof(ApproveOrder));
        log.LogInformation("Received an approval result.");
        ApprovalResult approvalResult = await GetApprovalResult(req);
        await client.Client.RaiseEventAsync(approvalResult.OrchestrationId!, "OrderApprovalResult", approvalResult.Approved ? "Approved" : "Rejected");
        log.LogInformation($"Approval Result for {approvalResult.OrchestrationId} is {approvalResult.Approved}");
        return req.CreateResponse(HttpStatusCode.OK);
    }

    private static async Task<ApprovalResult> GetApprovalResult(HttpRequestData req)
    {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var approvalResult = JsonConvert.DeserializeObject<ApprovalResult>(requestBody);
        if (approvalResult == null || approvalResult.OrchestrationId == null) throw new InvalidOperationException("Invalid approval");
        return approvalResult;
    }
}
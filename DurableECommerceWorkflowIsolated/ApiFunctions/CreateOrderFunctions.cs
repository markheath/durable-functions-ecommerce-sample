using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core.Serialization;
using DurableECommerceWorkflowIsolated.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DurableECommerceWorkflowIsolated.ApiFunctions;

public static class CreateOrderFunctions
{

    internal static readonly ObjectSerializer serializer = new JsonObjectSerializer(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });

    [Function(nameof(CreateOrder))]
    public static async Task<HttpResponseData> CreateOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous,
            "post", Route = null)]HttpRequestData req,
        [DurableClient] DurableTaskClient durableTaskClient,
        FunctionContext context)
    {
        var log = context.GetLogger(nameof(CreateOrder));
        log.LogInformation("Received a new order from website.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var order = JsonSerializer.Deserialize<Order>(requestBody);
        if (order == null) throw new InvalidOperationException("Failed to deserialize order from request body");

        // use shorter order ids for the webpage
        var r = new Random();
        order.Id = r.Next(10000, 100000).ToString();

        log.LogWarning($"Order {order.Id} for {order.ItemCount()} items, total {order.Total()}");

        var orchestrationId = await durableTaskClient.ScheduleNewOrchestrationInstanceAsync("O_ProcessOrder", order);
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(new { order.Id }, serializer);
        return resp;
    }

    [Function(nameof(NewPurchaseWebhook))]
    public static async Task<HttpResponseData> NewPurchaseWebhook(
        [HttpTrigger(AuthorizationLevel.Anonymous,
            "post", Route = null)]HttpRequestData req,
        [DurableClient] DurableTaskClient durableTaskClient,
        FunctionContext context)
    {
        var log = context.GetLogger(nameof(NewPurchaseWebhook));
        log.LogInformation("Received an order webhook.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var order = JsonSerializer.Deserialize<Order>(requestBody);
        if (order == null) throw new InvalidOperationException("Failed to deserialize order from request body");
        log.LogInformation($"Order is from {order.PurchaserEmail} for {order.ItemCount()} items, total {order.Total()}");

        var orchestrationId = await durableTaskClient.ScheduleNewOrchestrationInstanceAsync("O_ProcessOrder", order);
        return durableTaskClient.CreateCheckStatusResponse(req, orchestrationId);
    }
}

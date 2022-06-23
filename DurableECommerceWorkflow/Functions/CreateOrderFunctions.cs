using System;
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

public static class CreateOrderFunctions
{
    [FunctionName(nameof(CreateOrder))]
    public static async Task<IActionResult> CreateOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous,
            "post", Route = null)]HttpRequest req,
        [DurableClient] IDurableOrchestrationClient client,
        ILogger log)
    {
        log.LogInformation("Received a new order from website.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var order = JsonConvert.DeserializeObject<Order>(requestBody);

        // use shorter order ids for the webpage
        var r = new Random();
        order.Id = r.Next(10000, 100000).ToString();

        log.LogWarning($"Order {order.Id} for {order.ItemCount()} items, total {order.Total()}");

        var orchestrationId = await client.StartNewAsync("O_ProcessOrder", order);
        return new OkObjectResult(new { order.Id });
    }

    [FunctionName(nameof(NewPurchaseWebhook))]
    public static async Task<IActionResult> NewPurchaseWebhook(
        [HttpTrigger(AuthorizationLevel.Anonymous,
            "post", Route = null)]HttpRequest req,
        [DurableClient] IDurableOrchestrationClient client,
        ILogger log)
    {
        log.LogInformation("Received an order webhook.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var order = JsonConvert.DeserializeObject<Order>(requestBody);
        log.LogInformation($"Order is from {order.PurchaserEmail} for {order.ItemCount()} items, total {order.Total()}");

        var orchestrationId = await client.StartNewAsync("O_ProcessOrder", order);
        var statusUris = client.CreateHttpManagementPayload(orchestrationId);
        return new OkObjectResult(statusUris);
    }
}

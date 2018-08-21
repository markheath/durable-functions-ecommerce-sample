using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System;

namespace DurableECommerceWorkflow
{
    public static class CreateOrderFunctions
    {
        [FunctionName("CreateOrder")]
        public static async Task<IActionResult> CreateOrder(
            [HttpTrigger(AuthorizationLevel.Function,
                "post", Route = null)]HttpRequest req,
            [OrchestrationClient] DurableOrchestrationClient client,
            TraceWriter log)
        {
            log.Info("Received a new order from website.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var order = JsonConvert.DeserializeObject<Order>(requestBody);

            // use shorter order ids for the webpage
            var r = new Random();
            order.Id = r.Next(10000, 100000).ToString();

            log.Warning($"Order {order.Id} for product {order.ProductId} amount {order.Amount}");

            var orchestrationId = await client.StartNewAsync("O_ProcessOrder", order);
            return new OkObjectResult(new { order.Id });
        }

        [FunctionName("NewPurchaseWebhook")]
        public static async Task<IActionResult> NewPurchaseWebhook(
            [HttpTrigger(AuthorizationLevel.Function,
                "post", Route = null)]HttpRequest req,
            [OrchestrationClient] DurableOrchestrationClient client,
            TraceWriter log)
        {
            log.Info("Received an order webhook.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var order = JsonConvert.DeserializeObject<Order>(requestBody);
            log.Info($"Order is from {order.PurchaserEmail} for product {order.ProductId} amount {order.Amount}");

            var orchestrationId = await client.StartNewAsync("O_ProcessOrder", order);
            var statusUris = client.CreateHttpManagementPayload(orchestrationId);
            return new OkObjectResult(statusUris);
        }
    }
}

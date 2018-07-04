
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

    public static class StarterFunctions
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

            log.Info($"Order {order.Id} for product {order.ProductId} amount {order.Amount}");

            var orchestrationId = await client.StartNewAsync("O_ProcessOrder", order);
            return new OkObjectResult(new { order.Id });
        }

        [FunctionName("GetOrderStatus")]
        public static async Task<IActionResult> GetOrderStatus(
            [HttpTrigger(AuthorizationLevel.Function,
            "get", Route = "/orderstatus/{id}")]HttpRequest req,
            [OrchestrationClient] DurableOrchestrationClient client,
            [Table("todos", "TODO", "{id}", Connection = "AzureWebJobsStorage")] OrderEntity order,
            TraceWriter log, string id)
        {
            log.Info($"Checking status of order {id}");

            if (order == null)
            {
                return new NotFoundResult();
            }
            var status = await client.GetStatusAsync(order.OrchestrationId);
            
            return new OkObjectResult(status);
        }

        public static async Task<IActionResult> ApproveOrderById(
            [HttpTrigger(AuthorizationLevel.Function,
            "get", Route = "/approve/{id}")]HttpRequest req,
            [OrchestrationClient] DurableOrchestrationClient client,
            [Table("todos", "TODO", "{id}", Connection = "AzureWebJobsStorage")] OrderEntity order,
            TraceWriter log, string id)
        {
            log.Info($"Setting approval status of order {id}");

            if (order == null)
            {
                return new NotFoundResult();
            }
            await client.RaiseEventAsync(order.OrchestrationId, "OrderApprovalResult", "Approved");
            
            return new OkResult();
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

        [FunctionName("ApproveOrder")]
        public static async Task<IActionResult> ApproveOrder(
            [HttpTrigger(AuthorizationLevel.Function,
            "post", Route = null)]HttpRequest req,
            [OrchestrationClient] DurableOrchestrationClient client,
            TraceWriter log)
        {
            log.Info("Received an approval result.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var approvalResult = JsonConvert.DeserializeObject<ApprovalResult>(requestBody);
            await client.RaiseEventAsync(approvalResult.OrchestrationId, "OrderApprovalResult", approvalResult.Approved ? "Approved" : "Rejected");
            log.Info($"Approval Result for {approvalResult.OrchestrationId} is {approvalResult.Approved}");
            return new OkResult();
        }
    }
}

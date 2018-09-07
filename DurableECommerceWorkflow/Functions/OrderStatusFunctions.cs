using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace DurableECommerceWorkflow
{
    public static class OrderStatusFunctions
    {
        [FunctionName("GetOrderStatus")]
        public static async Task<IActionResult> GetOrderStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous,
                "get", Route = "orderstatus/{id}")]HttpRequest req,
            [OrchestrationClient] DurableOrchestrationClient client,
            [Table(OrderEntity.TableName, OrderEntity.OrderPartitionKey, "{id}", Connection = "AzureWebJobsStorage")] OrderEntity order,
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

        [FunctionName("GetAllOrders")]
        public static async Task<IActionResult> GetAllOrders(
            [HttpTrigger(AuthorizationLevel.Anonymous,
                "get", Route = null)]HttpRequest req,
            [OrchestrationClient] DurableOrchestrationClient client,
            TraceWriter log)
        {
            log.Info("getting all orders.");
            // just get orders in the last couple of hours to keep manage screen simple
            // interested in orders of all statuses
            var statuses = await client.GetStatusAsync(DateTime.Today.AddHours(-2.0), null, 
                Enum.GetValues(typeof(OrchestrationRuntimeStatus)).Cast<OrchestrationRuntimeStatus>()
                );
            return new OkObjectResult(statuses);
        }
    }
}
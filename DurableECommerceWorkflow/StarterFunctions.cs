
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace DurableECommerceWorkflow
{

    public static class StarterFunctions
    {
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

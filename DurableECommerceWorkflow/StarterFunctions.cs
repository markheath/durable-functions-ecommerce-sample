
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
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, 
            "post", Route = null)]HttpRequest req, 
            TraceWriter log)
        {
            log.Info("Received an order webhook.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var order = JsonConvert.DeserializeObject<Order>(requestBody);
            log.Info($"Order is from {order.PurchaserEmail} for product {order.ProductId} amount {order.Amount}");

            return new OkObjectResult($"Thanks for your order");
        }
    }
}

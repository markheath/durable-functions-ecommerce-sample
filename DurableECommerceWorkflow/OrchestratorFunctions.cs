using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Threading.Tasks;

namespace DurableECommerceWorkflow
{
    public static class OrchestratorFunctions
    {
        [FunctionName("O_ProcessOrder")]
        public static async Task<object> ProcessOrder(
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
        {
            var order = ctx.GetInput<Order>();

            if (!ctx.IsReplaying)
                log.Info($"Processing order for {order.ProductId}");

            return "Order accepted";
        }
    }
}

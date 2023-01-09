using System.Net;
using System.Text.Json;
using Azure.Data.Tables;
using DurableECommerceWorkflowIsolated.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DurableECommerceWorkflowIsolated.ApiFunctions
{
    public static class OrderStatusFunctions
    {
        [Function(nameof(GetOrderStatus))]
        public static async Task<HttpResponseData> GetOrderStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous,
                "get", Route = "orderstatus/{id}")]HttpRequestData req,
            [DurableClient] DurableClientContext clientContext,
            //[TableInput(OrderEntity.TableName, OrderEntity.OrderPartitionKey, "{id}", Connection = "AzureWebJobsStorage")] OrderEntity order, - fails with converting string to OrderEntity
            FunctionContext functionContext, string id)
        {
            var log = functionContext.GetLogger(nameof(GetOrderStatus));
            log.LogInformation($"Checking status of order {id}");

            var tableServiceClient = functionContext.InstanceServices.GetRequiredService<TableServiceClient>();
            var tableClient = tableServiceClient.GetTableClient(OrderEntity.TableName);
            var orderResp = await tableClient.GetEntityIfExistsAsync<OrderEntity>(OrderEntity.OrderPartitionKey, id);

            if (!orderResp.HasValue)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }
            var order = orderResp.Value;

            var status = await clientContext.Client.GetInstanceMetadataAsync(order.OrchestrationId, true);
            if (status == null)
            {
                log.LogError($"Could not fetch instance metadata for order {order.OrchestrationId}");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
            var statusObj = new
            {
                status.InstanceId,
                CreatedTime = status.CreatedAt,
                CustomStatus = DeserializeCustomStatus(status.SerializedCustomStatus),
                Output = DeserializeOutput(status.SerializedOutput),
                LastUpdatedTime = status.LastUpdatedAt,
                status.RuntimeStatus,
                order.Items,
                order.Amount,
                PurchaserEmail = order.Email
            };

            var resp = req.CreateResponse(HttpStatusCode.OK);
            await resp.WriteAsJsonAsync(statusObj, CreateOrderFunctions.serializer);
            return resp;
        }

        [Function("DeleteOrder")]
        public static async Task<HttpResponseData> DeleteOrder(
                [HttpTrigger(AuthorizationLevel.Anonymous,
                "delete", Route = "order/{id}")]HttpRequestData req,
                [DurableClient] DurableClientContext clientContext,
                //[TableInput(OrderEntity.TableName, OrderEntity.OrderPartitionKey, "{id}", Connection = "AzureWebJobsStorage")] OrderEntity order,
                FunctionContext functionContext, string id)
        {
            var log = functionContext.GetLogger(nameof(DeleteOrder));

            var tableServiceClient = functionContext.InstanceServices.GetRequiredService<TableServiceClient>();
            var tableClient = tableServiceClient.GetTableClient(OrderEntity.TableName);
            var orderResp = await tableClient.GetEntityIfExistsAsync<OrderEntity>(OrderEntity.OrderPartitionKey, id);

            if (!orderResp.HasValue)
            {
                // could be that there is an orchestration for this order, but its not in the order table anymore
                log.LogWarning($"Cannot find order {id}");
                return req.CreateResponse(HttpStatusCode.NotFound);
            }
            var order = orderResp.Value;
            log.LogInformation($"Deleting order {id}");

            var status = await clientContext.Client.GetInstanceMetadataAsync(order.OrchestrationId, false);
            if (status?.RuntimeStatus == OrchestrationRuntimeStatus.Running)
            {
                log.LogWarning($"Order is in progress {id}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }
            await clientContext.Client.PurgeInstanceMetadataAsync(order.OrchestrationId);

            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function("GetAllOrders")]
        public static async Task<HttpResponseData> GetAllOrders(
            [HttpTrigger(AuthorizationLevel.Anonymous,
                "get", Route = null)]HttpRequestData req,
            [DurableClient] DurableClientContext clientContext,
            FunctionContext functionContext)
        {
            var log = functionContext.GetLogger(nameof(GetAllOrders));
            log.LogInformation("getting all orders.");
            // just get orders in the last couple of hours to keep manage screen simple
            // interested in orders of all statuses
            var metadata = await clientContext.Client.GetInstances(
                new OrchestrationQuery(DateTime.Today.AddHours(-8.0),
                FetchInputsAndOutputs: true,
                Statuses: Enum.GetValues(typeof(OrchestrationRuntimeStatus)).Cast<OrchestrationRuntimeStatus>()))
                .ToListAsync();
            var statuses = metadata.Select(status => new
            {
                status.InstanceId,
                CreatedTime = status.CreatedAt,
                CustomStatus = DeserializeCustomStatus(status.SerializedCustomStatus),
                Output = DeserializeOutput(status.SerializedOutput),
                Input = DeserializeInput(status.SerializedInput),
                LastUpdatedTime = status.LastUpdatedAt,
                status.RuntimeStatus
            }).ToList();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(statuses, CreateOrderFunctions.serializer);
            return response;
        }

        private static object? DeserializeOutput(string? serializedOutput)
        {
            if (string.IsNullOrEmpty(serializedOutput)) return null;
            if (serializedOutput.StartsWith("{"))
                return JsonSerializer.Deserialize<OrderResult>(serializedOutput);
            return serializedOutput; // serialized output might not actually be serialized JSON at all!
        }

        private static Order? DeserializeInput(string? serializedInput)
        {
            return JsonSerializer.Deserialize<Order>(serializedInput ?? "{}");
        }

        private static string DeserializeCustomStatus(string? serializedCustomStatus)
        {
            if (string.IsNullOrEmpty(serializedCustomStatus)) return "";
            var s = JsonSerializer.Deserialize<string>(serializedCustomStatus);
            return s ?? "";
        }
    }
}
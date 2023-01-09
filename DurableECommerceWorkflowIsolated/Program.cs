using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SendGrid.Extensions.DependencyInjection;

var sendGridKey = Environment.GetEnvironmentVariable("SendGridKey");
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults() // supposedly setting camelCase
    .ConfigureServices(services =>
    {
        services.Configure<JsonSerializerOptions>(options =>
        {
            // options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; // https://stackoverflow.com/questions/60574428/how-to-configure-a-default-jsonserializeroptions-system-text-json-to-be-used-b
            options.Converters.Add(new JsonStringEnumConverter());
        });
        services.AddSendGrid(options => options.ApiKey = sendGridKey);
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddBlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            clientBuilder.AddTableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            // clientBuilder.UseCredential(new DefaultAzureCredential());
        });

    })
    .Build();

host.Run();
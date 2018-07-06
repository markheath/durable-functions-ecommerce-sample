# Durable Functions E-Commerce Sample

In this sample application, when an order webhook is received (by the `NewPurchaseWebhook` function), we initiate an order processing workflow.
First it stores a new row in our "database" (using Azure Table storage for simplicity). Then in parallel it creates a PDF and MP4 file (actually just text files) in a blob storage account and generates SAS tokens to download them. Finally, it sends out an email to the purchaser. 


Calling the starter function from PowerShell: 

```powershell
$orderInfo = "{ productId: 'azure-functions', amount:24, purchaserEmail:'your@email.com' }"
$webhookUri = "http://localhost:7071/api/NewPurchaseWebhook"
$statusUris = Invoke-RestMethod -Method Post -Body $orderInfo -Uri $webhookUri

# check the status of the workflow
Invoke-RestMethod -Uri $statusUris.StatusQueryGetUri
```

To simulate an error in PDF generation, use:
```powershell
$orderInfoErr = "{ productId: 'error', amount:24, purchaserEmail:'your@email.com' }"
```

To simulate an order needing approval, use:

```powershell
$orderInfo = "{ productId: 'durable-functions', amount:3000, purchaserEmail:'your@email.com' }"
```

To send an approval to a specific orchestration:

```powershell
function Approve-Order {
    Param ([String]$orchestrationId)
    $approvalResult = "{ orchestrationId: '" + $orchestrationId + "', approved:true }"
    $approveOrderUri = "http://localhost:7071/api/ApproveOrder"
    Invoke-RestMethod -Method Post -Body $approvalResult -Uri $approveOrderUri
}
Approve-Order -orchestrationId <<your-orchestration-id>>
```

To run the application locally, you'll need to set up your local.settings.json file, which is not checked into source control. An example is shown below:

```javascript
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "AzureWebJobsDashboard": "UseDevelopmentStorage=true",
        "SendGridKey": "<<your sendgrid key here>>",
        "ApproverEmail": "<<your email address here>>",
        "SenderEmail": "any@example.email",
        "Host": "http://localhost:7071",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet",
        "AZURE_FUNCTION_PROXY_DISABLE_LOCAL_CALL": "true", // https://github.com/Azure/azure-functions-core-tools/issues/319
        "WEB_HOST": "http://localhost:54045"
    }
}
```
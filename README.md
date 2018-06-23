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
$orderInfoErr = "{ productId: 'durable-functions', amount:3000, purchaserEmail:'your@email.com' }"
```

To send an approval to a specific orchestration:

```powershell
$orchestrationId = "" # todo: insert orchestration id 
$approvalResult = "{ orchestrationId: '" + $orchestrationId + "', approved:true }"
$approveOrderUri = "http://localhost:7071/api/ApproveOrder"
Invoke-RestMethod -Method Post -Body $approvalResult -Uri $approveOrderUri
```

To run the application locally, you'll need to set up your local.settings.json file, which is not checked into source control. An example

```javascript
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "AzureWebJobsDashboard": "UseDevelopmentStorage=true"
    }
}
```
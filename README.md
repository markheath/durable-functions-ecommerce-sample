# Durable Functions E-Commerce Sample

In this sample application, when an order webhook is received (by the `NewPurchaseWebhook` function), we initiate an order processing workflow.

- First, it stores a new row in our "database" (using Azure Table storage for simplicity). 
- Second, it requests approval if the value of the order is greater than a certain amount. This involves sending an email to an administrator and them using the "management" web-page to approve or reject the order.
- Then in parallel it creates a 'PDF' (actually just text file) for each item ordered in a blob storage account and generates SAS tokens to download them. 
- Finally, it sends out an email to the purchaser containing the download SAS tokens.

### Local Application Settings

To run the application locally, you'll need to set up your `local.settings.json` file, which is not checked into source control. An example is shown below:

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

### Testing from the Web UI

If you set both `DurableECommerceWeb` and `DurableECommerceWorkflow` projects as startup projects, then you will be able to proxy through from the Azure Functions project to the website.

Visiting `localhost:7071` will take you to a page where you can add items to your shopping cart and create an order.

By visiting `localhost:7071/orderStatus/<orderId>` you can view the current status of the order.

And visiting `localhost:7071/manage` takes you to an order management dashboard that lets administrators approve or reject orders for large amounts, as well as purge order history for completed orders.

### Testing from PowerShell

Calling the starter function from PowerShell: 

```powershell
$orderInfo = "{ items: [{productId: 'azure-functions', amount:24}], purchaserEmail:'your@email.com' }"
$webhookUri = "http://localhost:7071/api/NewPurchaseWebhook"
$statusUris = Invoke-RestMethod -Method Post -Body $orderInfo -Uri $webhookUri

# check the status of the workflow
Invoke-RestMethod -Uri $statusUris.StatusQueryGetUri
```

To simulate an error in PDF generation, use:
```powershell
$orderInfoErr = "{ items: [{productId: 'error', amount:24}], purchaserEmail:'your@email.com' }"
```

To simulate an order needing approval, use:

```powershell
$orderInfo = "{ items: [{productId: 'durable-functions', amount:3000}], purchaserEmail:'your@email.com' }"
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


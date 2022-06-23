# Durable Functions E-Commerce Sample

In this sample application, when an order webhook is received (by the `NewPurchaseWebhook` function), we initiate an order processing workflow.

- First, it stores a new row in our "database" (using Azure Table storage for simplicity). 
- Second, it requests approval if the value of the order is greater than a certain amount. This involves sending an email to an administrator and them using the "management" web-page to approve or reject the order.
- Then in parallel it creates a 'PDF' (actually just text file) for each item ordered in a blob storage account and generates SAS tokens to download them. 
- Finally, it sends out an email to the purchaser containing the download SAS tokens.
    - (note you need your own SendGrid API key to actually send emails)

### Local Application Settings

To run the application locally, you'll need to set up your `local.settings.json` file, which is not checked into source control. An example is shown below:

```javascript
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "SendGridKey": "<<your sendgrid API key here>>",
        "ApproverEmail": "durable-funcs-approver@mailinator.com",
        "SenderEmail": "any@example.email",
        "Host": "http://localhost:7071",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    },
    "Host": {
        "CORS": "*"
    }
}
```

Using mailinator for testing and demos:
- https://www.mailinator.com/v4/public/inboxes.jsp?to=durable-funcs-approver
- https://www.mailinator.com/v4/public/inboxes.jsp?to=durable-funcs-customer

### Running in the cloud
- You need to run the static website (can run anywhere - Azure App Service is probably easiest). 
- You need to run the
- You will need to update `baseUrl` in the JavaScript files to point to the fucntion app
- You will need the Azure Function app to accept CORS requests from the website
- You will need to set up Application Settings for the function app - `SendGridKey`, `ApproverEmail`, `SenderEmail`

### Testing from the Web UI

Set both `DurableECommerceWeb` and `DurableECommerceWorkflow` projects as startup projects

Visiting `https://localhost:5001/` will take you to a page where you can add items to your shopping cart and create an order.

By visiting `https://localhost:5001/orderStatus.html?id=<orderId>` you can view the current status of the order.

And visiting `https://localhost:5001/manage` takes you to an order management dashboard that lets administrators approve or reject orders for large amounts, as well as purge order history for completed orders.

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


# Durable Functions E-Commerce Sample

Calling the starter function from PowerShell: 

```powershell
$orderInfo = "{ productId: '1', amount:24, purchaserEmail:'buyer@example.com' }"
$webhookUri = "http://localhost:7071/api/NewPurchaseWebhook"
$statusUris = Invoke-RestMethod -Method Post -Body $orderInfo -Uri $webhookUri

# check the status of the workflow
Invoke-RestMethod -Uri $statusUris.StatusQueryGetUri
```

To simulate an error, use:
```
$orderInfoErr = "{ productId: 'error', amount:24, purchaserEmail:'buyer@example.com' }"
```

To simulate an order needing approval, use:
```
$orderInfoErr = "{ productId: '2', amount:3000, purchaserEmail:'buyer@example.com' }"
```

To send an approval to a specific orchestration
```
$orchestrationId = "" # todo: insert orchestration id 
$approvalResult = "{ orchestrationId: '" + $orchestrationId + "', approved:true }"
$approveOrderUri = "http://localhost:7071/api/ApproveOrder"
Invoke-RestMethod -Method Post -Body $approvalResult -Uri $approveOrderUri
```
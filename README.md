# Durable Functions E-Commerce Sample

Calling the starter function from PowerShell: 

```powershell
$orderInfo = "{ productId: '1', amount:24, purchaserEmail:'buyer@example.com' }"
$webhookUri = "http://localhost:7071/api/NewPurchaseWebhook"
Invoke-RestMethod -Method Post -Body $orderInfo -Uri $webhookUri
```
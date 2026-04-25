# Driver Wallet Contract

## Status

- `implemented`

## Purpose

هذا الملف يشرح الـ wallet backend الحقيقية للمندوب:

- summary
- transactions
- payment methods
- withdrawal requests

## Endpoints

### 1. Get Wallet Summary

- `GET /api/drivers/wallet`

Example response:

```json
{
  "currentBalance": 1500.0,
  "availableToWithdraw": 1500.0,
  "pendingBalance": 200.0,
  "todayEarnings": 180.0,
  "weekEarnings": 760.0,
  "monthEarnings": 2400.0,
  "recentTransactions": [
    {
      "id": "33333333-3333-3333-3333-333333333333",
      "type": "Credit",
      "direction": "IN",
      "amount": 120.0,
      "description": "Delivery earning",
      "referenceType": "order",
      "referenceId": "44444444-4444-4444-4444-444444444444",
      "createdAtUtc": "2026-04-25T10:00:00Z"
    }
  ],
  "paymentMethods": [
    {
      "id": "55555555-5555-5555-5555-555555555555",
      "type": "BankAccount",
      "accountHolderName": "Ahmed Driver",
      "providerName": "Al Rajhi",
      "maskedLabel": "Al Rajhi ****1234",
      "isPrimary": true,
      "isVerified": true
    }
  ],
  "withdrawalSummary": {
    "pendingCount": 1,
    "pendingAmount": 300.0,
    "totalRequests": 4
  }
}
```

### 2. Get Wallet Transactions

- `GET /api/drivers/wallet/transactions?page=1&pageSize=20`

response:

```json
{
  "items": [
    {
      "id": "33333333-3333-3333-3333-333333333333",
      "type": "Credit",
      "direction": "IN",
      "amount": 120.0,
      "description": "Delivery earning",
      "referenceType": "order",
      "referenceId": "44444444-4444-4444-4444-444444444444",
      "createdAtUtc": "2026-04-25T10:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

### 3. Get Payment Methods

- `GET /api/drivers/wallet/payment-methods`

### 4. Create Payment Method

- `POST /api/drivers/wallet/payment-methods`

body:

```json
{
  "type": "BankAccount",
  "accountHolderName": "Ahmed Driver",
  "accountIdentifier": "SA0380000000608010167519",
  "providerName": "Al Rajhi",
  "isPrimary": true
}
```

supported `type` values:

- `BankAccount`
- `DebitCard`
- `InstantTransfer`

### 5. Update Payment Method

- `PUT /api/drivers/wallet/payment-methods/{id}`

body:

```json
{
  "type": "BankAccount",
  "accountHolderName": "Ahmed Driver",
  "accountIdentifier": "SA0380000000608010167519",
  "providerName": "Al Rajhi"
}
```

### 6. Delete Payment Method

- `DELETE /api/drivers/wallet/payment-methods/{id}`

### 7. Make Payment Method Primary

- `POST /api/drivers/wallet/payment-methods/{id}/make-primary`

### 8. Create Withdrawal Request

- `POST /api/drivers/wallet/withdrawals`

body:

```json
{
  "paymentMethodId": "55555555-5555-5555-5555-555555555555",
  "amount": 300.0
}
```

Notes:

- لو `paymentMethodId = null` أو غير مرسلة، الـ backend يستخدم الـ primary method
- لو لا توجد primary method، الطلب يفشل
- لو الرصيد أقل من المبلغ، الطلب يفشل

example response:

```json
{
  "id": "66666666-6666-6666-6666-666666666666",
  "amount": 300.0,
  "status": "Pending",
  "transferReference": null,
  "failureReason": null,
  "createdAtUtc": "2026-04-25T10:30:00Z",
  "processedAtUtc": null,
  "paymentMethod": {
    "id": "55555555-5555-5555-5555-555555555555",
    "type": "BankAccount",
    "accountHolderName": "Ahmed Driver",
    "providerName": "Al Rajhi",
    "maskedLabel": "Al Rajhi ****1234",
    "isPrimary": true,
    "isVerified": true
  }
}
```

### 9. Get Withdrawals

- `GET /api/drivers/wallet/withdrawals?page=1&pageSize=20`

## Important Notes

- `availableToWithdraw` في الـ implementation الحالي = `currentBalance`
- إنشاء withdrawal request يقوم بعمل:
  - `wallet.Hold(amount)`
  - transaction من نوع `Hold`
  - record داخل `DriverWithdrawalRequests`
- لا يوجد payout instant rail في v1
- payment methods تُدار من المندوب نفسه بالكامل

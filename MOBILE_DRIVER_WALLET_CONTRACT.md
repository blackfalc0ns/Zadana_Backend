# Mobile Integration Guide: Driver Wallet

This document is the mobile contract for the driver wallet feature.
It covers wallet summary, transaction history, payout methods, and withdrawal requests.

## Base Rules

- Base path: `api/drivers/wallet`
- Auth: `Bearer token` for an authenticated driver
- Content type for write requests: `application/json`
- All money values are decimal numbers
- All timestamps are UTC ISO-8601 strings

## 1. Get Wallet Summary

**Endpoint**

`GET /api/drivers/wallet`

**Response 200**

```json
{
  "currentBalance": 245.50,
  "availableToWithdraw": 245.50,
  "pendingBalance": 40.00,
  "todayEarnings": 35.00,
  "weekEarnings": 120.00,
  "monthEarnings": 410.00,
  "recentTransactions": [
    {
      "id": "7b4585f8-1b13-4c4b-8f03-0d4c301f6ce1",
      "type": "OrderRevenue",
      "direction": "IN",
      "amount": 25.00,
      "description": "Delivery fee from order 123",
      "referenceType": "OrderRevenue",
      "referenceId": null,
      "createdAtUtc": "2026-05-03T12:10:00Z"
    }
  ],
  "paymentMethods": [
    {
      "id": "1c12f837-d901-4d3a-bf0f-0f4b0170bb8d",
      "type": "BankAccount",
      "accountHolderName": "Ahmed Ali",
      "providerName": "Al Rajhi",
      "maskedLabel": "Al Rajhi ****4321",
      "isPrimary": true,
      "isVerified": true
    }
  ],
  "withdrawalSummary": {
    "pendingCount": 1,
    "pendingAmount": 40.00,
    "totalRequests": 3
  }
}
```

**Notes**

- `recentTransactions` returns the latest 10 transactions only.
- `availableToWithdraw` currently equals `currentBalance`.
- `pendingBalance` is the amount currently held for pending withdrawals.

## 2. Get Full Transaction History

**Endpoint**

`GET /api/drivers/wallet/transactions?page=1&pageSize=20`

**Query params**

- `page`: minimum `1`
- `pageSize`: min `1`, max `100`

**Response 200**

```json
{
  "items": [
    {
      "id": "7b4585f8-1b13-4c4b-8f03-0d4c301f6ce1",
      "type": "Hold",
      "direction": "OUT",
      "amount": 40.00,
      "description": "Driver withdrawal request submitted",
      "referenceType": "DriverWithdrawal",
      "referenceId": "2d72813e-b6c7-4b8a-bd4e-a7fc9f44172d",
      "createdAtUtc": "2026-05-03T12:20:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 12
}
```

**Common transaction types**

- `OrderRevenue`
- `Hold`
- `Release`
- `Payout`
- `Adjustment`

**Common directions**

- `IN`
- `OUT`

## 3. Get Payout Methods

**Endpoint**

`GET /api/drivers/wallet/payment-methods`

**Response 200**

```json
[
  {
    "id": "1c12f837-d901-4d3a-bf0f-0f4b0170bb8d",
    "type": "BankAccount",
    "accountHolderName": "Ahmed Ali",
    "providerName": "Al Rajhi",
    "maskedLabel": "Al Rajhi ****4321",
    "isPrimary": true,
    "isVerified": true
  }
]
```

**Supported `type` values**

- `BankAccount`
- `DebitCard`
- `InstantTransfer`

## 4. Create Payout Method

**Endpoint**

`POST /api/drivers/wallet/payment-methods`

**Request body**

```json
{
  "type": "BankAccount",
  "accountHolderName": "Ahmed Ali",
  "accountIdentifier": "1234567890123456",
  "providerName": "Al Rajhi",
  "isPrimary": true
}
```

**Response 200**

Returns the created `DriverPayoutMethodDto`.

**Validation**

- `type` is required
- `accountHolderName` is required
- `accountIdentifier` is required

## 5. Update Payout Method

**Endpoint**

`PUT /api/drivers/wallet/payment-methods/{id}`

**Request body**

```json
{
  "type": "DebitCard",
  "accountHolderName": "Ahmed Ali",
  "accountIdentifier": "4111111111111111",
  "providerName": "Mada"
}
```

**Response 200**

Returns the updated `DriverPayoutMethodDto`.

## 6. Delete Payout Method

**Endpoint**

`DELETE /api/drivers/wallet/payment-methods/{id}`

**Response 204**

No content.

**Important behavior**

- If this method is the primary one and another method exists, another method becomes primary automatically.
- If this method is linked to old or current withdrawal requests, deletion is rejected.

**Business error**

```json
{
  "error_code": "DRIVER_PAYOUT_METHOD_IN_USE",
  "message": "This payout method cannot be deleted because it is linked to withdrawal requests."
}
```

## 7. Make Payout Method Primary

**Endpoint**

`POST /api/drivers/wallet/payment-methods/{id}/make-primary`

**Response 200**

Returns the updated `DriverPayoutMethodDto`.

## 8. Create Withdrawal Request

**Endpoint**

`POST /api/drivers/wallet/withdrawals`

**Request body**

```json
{
  "paymentMethodId": "1c12f837-d901-4d3a-bf0f-0f4b0170bb8d",
  "amount": 40.00
}
```

**Notes**

- `paymentMethodId` is optional.
- If `paymentMethodId` is omitted, backend uses the primary payout method.
- `amount` must be greater than `0`.

**Response 200**

```json
{
  "id": "2d72813e-b6c7-4b8a-bd4e-a7fc9f44172d",
  "amount": 40.00,
  "status": "Pending",
  "transferReference": null,
  "failureReason": null,
  "createdAtUtc": "2026-05-03T12:20:00Z",
  "processedAtUtc": null,
  "paymentMethod": {
    "id": "1c12f837-d901-4d3a-bf0f-0f4b0170bb8d",
    "type": "BankAccount",
    "accountHolderName": "Ahmed Ali",
    "providerName": "Al Rajhi",
    "maskedLabel": "Al Rajhi ****4321",
    "isPrimary": true,
    "isVerified": true
  }
}
```

**Wallet behavior after creating withdrawal**

- `currentBalance` decreases immediately
- `pendingBalance` increases by the same amount
- a transaction with `type = "Hold"` and `direction = "OUT"` is added

## 9. Get Withdrawal Requests

**Endpoint**

`GET /api/drivers/wallet/withdrawals?page=1&pageSize=20`

**Response 200**

```json
{
  "items": [
    {
      "id": "2d72813e-b6c7-4b8a-bd4e-a7fc9f44172d",
      "amount": 40.00,
      "status": "Pending",
      "transferReference": null,
      "failureReason": null,
      "createdAtUtc": "2026-05-03T12:20:00Z",
      "processedAtUtc": null,
      "paymentMethod": {
        "id": "1c12f837-d901-4d3a-bf0f-0f4b0170bb8d",
        "type": "BankAccount",
        "accountHolderName": "Ahmed Ali",
        "providerName": "Al Rajhi",
        "maskedLabel": "Al Rajhi ****4321",
        "isPrimary": true,
        "isVerified": true
      }
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 3
}
```

**Possible withdrawal status values**

- `Pending`
- `Processing`
- `Paid`
- `Failed`
- `Cancelled`

## Error Cases Mobile Should Handle

### Missing body

```json
{
  "error_code": "INVALID_REQUEST_BODY",
  "message": "Request body is required."
}
```

### Missing payout method fields

```json
{
  "error_code": "INVALID_ACCOUNT_IDENTIFIER",
  "message": "Account identifier is required."
}
```

Possible codes in this group:

- `INVALID_DRIVER_PAYOUT_METHOD_TYPE`
- `INVALID_ACCOUNT_HOLDER_NAME`
- `INVALID_ACCOUNT_IDENTIFIER`

### Unsupported payout method type

```json
{
  "error_code": "INVALID_DRIVER_PAYOUT_METHOD_TYPE",
  "message": "Unsupported payout method type."
}
```

### No payout method available for withdrawal

```json
{
  "error_code": "DRIVER_PAYOUT_METHOD_REQUIRED",
  "message": "Add a primary payout method before requesting a withdrawal."
}
```

### Selected payout method does not belong to this driver

```json
{
  "error_code": "DRIVER_PAYOUT_METHOD_NOT_FOUND",
  "message": "The selected payout method was not found for this driver."
}
```

### Insufficient balance

```json
{
  "error_code": "INSUFFICIENT_WITHDRAWABLE_BALANCE",
  "message": "Withdrawal amount exceeds available balance."
}
```

### Cannot delete payout method because it has withdrawal history

```json
{
  "error_code": "DRIVER_PAYOUT_METHOD_IN_USE",
  "message": "This payout method cannot be deleted because it is linked to withdrawal requests."
}
```

## Mobile UX Recommendations

- Refresh wallet summary after:
  - creating a payout method
  - updating the primary method
  - creating a withdrawal request
- When a withdrawal is rejected by admin, expect a wallet transaction with:
  - `type = "Release"`
  - `direction = "IN"`
- Use `maskedLabel` for display in the payout method picker instead of raw account identifier.
- If `paymentMethodId` is not sent, clearly show which primary method will be used before confirming the withdrawal.

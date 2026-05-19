# Mobile Payment Flow Response Update

## Status

Implemented on backend.

## Why This Changed

Bank transfer is not an instant in-app payment like card/Moyasar.

The order is created successfully, but payment remains pending until the bank transfer is confirmed by webhook/admin. Mobile must not treat a bank-transfer order as paid just because `POST /api/orders` returned `200 OK`.

## New Fields In `payment`

`POST /api/orders` now returns extra fields inside `payment`:

```json
{
  "payment_flow": "manual_bank_transfer",
  "is_paid": false,
  "requires_customer_action": true,
  "customer_action": "show_bank_transfer_instructions",
  "confirmation_mode": "bank_transfer_webhook"
}
```

## Card / Moyasar Flow

When:

```json
"payment_flow": "online_gateway"
```

Mobile should:

1. Render Moyasar form from `payment.provider_config`.
2. Use `provider_config.publishableKey`, `amount`, `currency`, `callbackUrl`, `methods`, `supportedNetworks`, and `metadata`.
3. After Moyasar returns provider payment id, call:

```http
POST /api/payments/moyasar/confirm
```

Body:

```json
{
  "id": "moyasar_provider_payment_id"
}
```

4. Show success only when backend returns/refreshes `payment_status = "paid"`.

Expected payment fields:

```json
{
  "provider": "moyasar",
  "payment_flow": "online_gateway",
  "is_paid": false,
  "requires_customer_action": true,
  "customer_action": "render_payment_form",
  "confirmation_mode": "provider_payment_id"
}
```

## Manual Bank Transfer Flow

When:

```json
"payment_flow": "manual_bank_transfer"
```

Mobile should:

1. Do not open Moyasar/card form.
2. Show a screen like: "Order created. Awaiting bank transfer."
3. Display bank details from `payment.provider_config`.
4. Display/copy the exact amount and reference.
5. Keep order as pending until backend confirms the transfer.
6. Refresh `GET /api/orders/{orderId}` or active orders until `payment_status = "paid"`.

Example:

```json
{
  "payment": {
    "provider": "banktransfer",
    "status": "pending",
    "provider_reference": "ZDN05193450E4A06aa8baab",
    "provider_config": {
      "bankName": "Test Bank",
      "accountHolderName": "Zadana Test Account",
      "iban": "SA0380000000608010167519",
      "accountNumber": "608010167519",
      "countryCode": "SA",
      "city": "Riyadh",
      "reference": "ZDN05193450E4A06aa8baab",
      "amount": 71.17,
      "currency": "SAR",
      "expiresAtUtc": "2026-05-20T16:11:24.2070567Z",
      "webhookDriven": true
    },
    "payment_flow": "manual_bank_transfer",
    "is_paid": false,
    "requires_customer_action": true,
    "customer_action": "show_bank_transfer_instructions",
    "confirmation_mode": "bank_transfer_webhook"
  }
}
```

## Fallback For Older Responses

If the new fields are missing:

```dart
if (payment.provider == 'moyasar') {
  paymentFlow = 'online_gateway';
  customerAction = 'render_payment_form';
}

if (payment.provider == 'banktransfer') {
  paymentFlow = 'manual_bank_transfer';
  customerAction = 'show_bank_transfer_instructions';
}
```

## Important Rules

- `POST /api/orders` with bank transfer means order created, not paid.
- Do not show paid/success screen for bank transfer until `payment_status == "paid"`.
- For card, do not show paid/success until `/api/payments/moyasar/confirm` succeeds or order refresh returns `payment_status == "paid"`.
- `payment.provider_reference` and `provider_config.reference` are the bank transfer reference used by backend matching.
- The reference can be shown as "Transaction Reference" or copied automatically with the bank details.

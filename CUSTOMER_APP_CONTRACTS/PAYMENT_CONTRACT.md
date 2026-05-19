# Customer Payment Contract

## Status

- `implemented`
- Online card provider: `Moyasar`
- Legacy Paymob URLs/endpoints must not be used by the customer app.

## Covered Endpoints

- `POST /api/orders`
- `POST /api/orders/{orderId}/retry-payment`
- `POST /api/payments/moyasar/confirm`
- `GET /api/payments/moyasar/verify?id=<moyasar_payment_id>` (Moyasar callback, not a normal authenticated customer API)

## Place Order

`POST /api/orders`

Body:

```json
{
  "vendor_id": "11111111-1111-1111-1111-111111111111",
  "address_id": "22222222-2222-2222-2222-222222222222",
  "delivery_slot_id": "asap",
  "payment_method": "card",
  "promo_code": null,
  "notes": null
}
```

Card response includes a `payment` object:

```json
{
  "payment": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "provider": "moyasar",
    "status": "pending",
    "iframe_url": "RenderMoyasarForm",
    "provider_reference": "",
    "provider_config": {
      "publishableKey": "pk_test_or_live",
      "amount": 12500,
      "currency": "SAR",
      "description": "Order ORD-000001",
      "callbackUrl": "https://your-api.com/api/payments/moyasar/verify",
      "methods": ["creditcard"],
      "supportedNetworks": ["mada", "visa", "mastercard"],
      "metadata": {
        "order_id": "33333333-3333-3333-3333-333333333333",
        "payment_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        "order_number": "ORD-000001"
      }
    }
  }
}
```

## Retry Payment

Use only when an existing order has `can_retry_payment = true`.

Endpoint:

`POST /api/orders/{orderId}/retry-payment`

Response shape is the same `payment` contract shown above.

## Mobile Rules

- Render Moyasar Form from `payment.provider_config`.
- Do not open `payment.iframe_url` as a URL; it is the action hint `RenderMoyasarForm`.
- Map `publishableKey` to Moyasar `publishable_api_key`.
- Map `callbackUrl` to Moyasar `callback_url`.
- Map `supportedNetworks` to Moyasar `supported_networks`.
- Do not send card data to Zadana backend.
- Do not store Moyasar secret keys in the app.
- When Moyasar returns a provider payment id, confirm it with Zadana backend before showing payment success.
- After the callback or after closing the payment screen, refresh `GET /api/orders/{orderId}` or `GET /api/orders/active`.
- Do not mark the order as paid locally unless Zadana returns `paymentStatus = "paid"`.

## Confirm Moyasar Payment

Use the Moyasar provider payment id, not Zadana's local `payment.id`.

`POST /api/payments/moyasar/confirm`

Body:

```json
{
  "id": "moyasar_payment_id"
}
```

Accepted aliases: `id`, `provider_payment_id`, `providerPaymentId`.

Expected successful paid response:

```json
{
  "message": "Payment confirmed successfully",
  "paymentId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "paymentStatus": "paid",
  "userId": "66666666-6666-6666-6666-666666666666",
  "orderId": "33333333-3333-3333-3333-333333333333",
  "orderStatus": "pending_vendor_acceptance",
  "alreadyConfirmed": false
}
```

`on_completed(payment)` can fire before the final redirect/3DS result. Store `payment.id`, call confirm when available, and call confirm again after the WebView reaches `callbackUrl` or when the customer closes the payment screen. The endpoint is idempotent.

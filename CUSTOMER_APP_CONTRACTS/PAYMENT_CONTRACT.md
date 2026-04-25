# Customer Payment Contract

## Status

- `implemented`

## Covered Endpoints

- `POST /api/orders`
- `POST /api/orders/{orderId}/retry-payment`

## Retry Payment

Use when an existing order has `can_retry_payment = true`.

### Endpoint

- `POST /api/orders/{orderId}/retry-payment`

Example response:

```json
{
  "message": "payment session created successfully",
  "payment": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "provider": "Paymob",
    "status": "Pending",
    "iframe_url": "https://accept.paymob.com/api/acceptance/iframes/123456?payment_token=xyz",
    "provider_reference": "PAYMOB-10245"
  }
}
```

## Place Order

- `POST /api/orders`

The place-order response may also include a `payment` object when the selected payment method requires an external payment session.

## Important Notes

- Do not assume cash-only behavior
- If `payment` is present, mobile should continue the external payment flow
- If `payment` is null, the order was created without an external payment session

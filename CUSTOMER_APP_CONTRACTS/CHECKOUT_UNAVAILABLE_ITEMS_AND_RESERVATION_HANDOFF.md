# Checkout Unavailable Items and Stock Reservation Handoff

## Status

- `implemented`
- Backend source of truth for mobile checkout changes related to:
  - selected vendor with partially unavailable cart items
  - confirming removal of unavailable items
  - stock reservation at order creation
  - payment reservation expiry handling

## Mobile Summary

When the customer selects a vendor and only some cart items are unavailable at that vendor, the checkout button should stay enabled.

Mobile must show a confirmation dialog before placing the order:

- Tell the customer that some items are unavailable at the selected vendor.
- If the customer cancels, do not place the order.
- If the customer confirms, call `POST /api/orders` with `remove_unavailable_items: true`.

The backend will remove the unavailable cart items and create the order only with the remaining available items.

## Endpoint: Checkout Summary

- `GET /api/checkout/summary?vendor_id={vendorId}&address_id={addressId}&payment_method={paymentMethod}`
- Auth: `Authorization: Bearer <access_token>`
- Policy: customer only
- Recommended query casing: snake_case
- CamelCase aliases are accepted, but mobile should prefer snake_case.

Relevant response fields:

```json
{
  "cart": {
    "items_count": 2,
    "total_quantity": 3,
    "items": [
      {
        "id": "11111111-1111-1111-1111-111111111111",
        "product_id": "22222222-2222-2222-2222-222222222222",
        "name": "Available Product",
        "quantity": 1,
        "price": 20,
        "total_price": 20
      }
    ],
    "has_unavailable_items": true,
    "unavailable_items_count": 1,
    "requires_unavailable_items_confirmation": true,
    "unavailable_items": [
      {
        "id": "33333333-3333-3333-3333-333333333333",
        "product_id": "44444444-4444-4444-4444-444444444444",
        "name": "Unavailable Product",
        "quantity": 2,
        "availability_status": "unavailable_at_selected_vendor"
      }
    ]
  },
  "delivery_check": {
    "can_proceed_to_checkout": true
  }
}
```

### Availability Status Values

- `unavailable_at_selected_vendor`
- `insufficient_stock`

Mobile can show both as unavailable. If the design supports a reason label:

- `unavailable_at_selected_vendor`: not sold by this vendor/address branch
- `insufficient_stock`: selected vendor does not have enough stock for the requested quantity

## Checkout Button Rules

Do not disable the checkout button only because:

- `cart.has_unavailable_items == true`
- `cart.requires_unavailable_items_confirmation == true`

Disable/block checkout only when normal checkout blockers exist, for example:

- `delivery_check.can_proceed_to_checkout == false`
- no valid address
- no supported payment method selected
- cart has no remaining available items

If `requires_unavailable_items_confirmation == true`, the button tap should open the confirmation dialog first.

Suggested dialog behavior:

- Title: `Some items are unavailable`
- Message: `The selected vendor cannot provide all cart items. Continue and remove unavailable items from the order?`
- Primary action: `Continue`
- Secondary action: `Cancel`

Suggested Arabic copy:

- Title: `في منتجات غير متوفرة`
- Message: `التاجر المختار لا يوفر كل منتجات العربة. هل تريد حذف المنتجات غير المتوفرة وإكمال الطلب؟`
- Primary action: `إكمال الطلب`
- Secondary action: `إلغاء`

## Endpoint: Place Order

- `POST /api/orders`
- Auth: `Authorization: Bearer <access_token>`
- Optional header: `X-Device-Id: <device_id>`

### Normal Place Order Request

When there are no unavailable items, omit `remove_unavailable_items` or send `false`.

```json
{
  "vendor_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "address_id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "delivery_slot_id": "asap",
  "payment_method": "card",
  "promo_code": null,
  "notes": null
}
```

### Confirmed Partial Checkout Request

After the customer confirms the dialog, send:

```json
{
  "vendor_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "address_id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "delivery_slot_id": "asap",
  "payment_method": "card",
  "promo_code": null,
  "notes": null,
  "remove_unavailable_items": true
}
```

Backend aliases also accepted for this flag:

- `removeUnavailableItems`
- `allowPartialCheckout`
- `confirmUnavailableItemsRemoval`

Mobile should use `remove_unavailable_items`.

## Place Order Response

```json
{
  "message": "Order placed successfully.",
  "order": {
    "id": "cccccccc-cccc-cccc-cccc-cccccccccccc",
    "created_at": "2026-07-17T00:00:00Z",
    "status": "pending_payment",
    "payment_method": "card",
    "payment_status": "pending",
    "total_price": 100
  },
  "payment": {
    "id": "dddddddd-dddd-dddd-dddd-dddddddddddd",
    "provider": "Moyasar",
    "status": "pending",
    "iframe_url": "https://...",
    "provider_reference": "pay_xxx",
    "is_paid": false,
    "requires_customer_action": true
  }
}
```

After success, mobile should treat unavailable items as removed from the order. Refresh cart/checkout state before showing the cart again.

## Error Handling

### `CART_UNAVAILABLE_ITEMS_CONFIRMATION_REQUIRED`

Meaning:

- Mobile attempted `POST /api/orders` while unavailable items exist.
- `remove_unavailable_items` was not `true`.

Mobile action:

1. Show the confirmation dialog.
2. If customer confirms, retry `POST /api/orders` with `remove_unavailable_items: true`.
3. If customer cancels, stay on checkout.

### `CART_ITEMS_UNAVAILABLE_AT_ADDRESS_BRANCH`

Meaning:

- The selected vendor/address branch cannot fulfill any usable checkout set for the cart.

Mobile action:

- Show an unavailable message.
- Ask the customer to choose another vendor/address or update the cart.
- Refresh checkout summary.

### `INSUFFICIENT_STOCK`

Meaning:

- Stock changed between summary and order creation.
- Backend could not reserve stock atomically.

Mobile action:

- Show stock unavailable message.
- Refresh checkout summary.
- Do not keep submitting the same stale order request.

### `ORDER_PAYMENT_RESERVATION_EXPIRED`

Meaning:

- Payment confirmation arrived after the order reservation expired/cancelled.
- The same payment/order cannot be confirmed anymore.

Mobile action:

- Show: `Order reservation expired. Please start checkout again.`
- Do not retry confirmation for the same payment.
- Refresh cart/order state.

## Stock Reservation Behavior

Backend now reserves stock when the order is created.

Stock is released when the order is cancelled/rejected/expired/refunded in backend-supported flows.

Mobile should not locally deduct or restore stock. Always trust the backend response and refresh summary/cart state after:

- order creation failure
- payment failure
- payment expiry
- user cancellation
- vendor rejection

## Mobile QA Checklist

- Select a vendor with all products available: order places normally.
- Select a vendor with some unavailable products: checkout button stays enabled.
- Tap checkout with unavailable products: confirmation dialog appears.
- Cancel dialog: no order is created.
- Confirm dialog: request sends `remove_unavailable_items: true`.
- Confirmed partial checkout: backend creates order with available items only.
- If backend returns `CART_UNAVAILABLE_ITEMS_CONFIRMATION_REQUIRED`, mobile shows the same dialog and retries only after user confirmation.
- If backend returns `INSUFFICIENT_STOCK`, mobile refreshes checkout summary.
- If payment confirmation returns `ORDER_PAYMENT_RESERVATION_EXPIRED`, mobile stops retrying that payment and asks the customer to restart checkout.

## Backend References

- `src/Zadana.Api/Modules/Orders/Controllers/CheckoutController.cs`
- `src/Zadana.Api/Modules/Orders/Controllers/OrdersController.cs`
- `src/Zadana.Api/Modules/Orders/Requests/CheckoutRequests.cs`
- `src/Zadana.Application/Modules/Checkout/Support/CheckoutSupport.cs`
- `src/Zadana.Application/Modules/Checkout/Commands/PlaceCheckoutOrder/PlaceCheckoutOrderCommand.cs`
- `src/Zadana.Application/Modules/Orders/Services/OrderInventoryWorkflowService.cs`
- `src/Zadana.Api/BackgroundJobs/PendingPaymentExpirationWorker.cs`

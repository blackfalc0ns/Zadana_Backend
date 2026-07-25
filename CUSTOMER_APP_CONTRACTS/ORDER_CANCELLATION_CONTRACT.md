# Customer Order Cancellation Contract

## Status

- `implemented`

## Purpose

This contract describes customer-initiated order cancellation, including pickup-specific approval behavior.

## Endpoints

### Get Cancellation Reasons

- `GET /api/orders/cancellation-reasons`

Example response:

```json
[
  {
    "code": "changed_my_mind",
    "label": "Changed my mind",
    "requires_note": false
  },
  {
    "code": "other",
    "label": "Other",
    "requires_note": true
  }
]
```

### Cancel Order

- `POST /api/orders/{orderId}/cancel`

Request body:

```json
{
  "reason_code": "changed_my_mind",
  "reason": null,
  "note": null
}
```

Accepted aliases:

- `reasonCode`
- `note`

Validation rules:

- send either `reason_code` or legacy `reason`
- when `reason_code = "other"`, `note` is required

## Delivery Cancellation Behavior

Delivery orders can be cancelled immediately only while status is one of:

- `pending_vendor_acceptance`
- `accepted`
- `preparing`

Successful immediate cancellation response:

```json
{
  "message": "Order cancelled successfully.",
  "order": {
    "id": "44444444-4444-4444-4444-444444444444",
    "status": "cancelled"
  }
}
```

If the order is outside the cancellable window, backend returns `ORDER_CANNOT_BE_CANCELLED`.

## Pickup Cancellation Behavior

Pickup orders use a two-stage cancellation model.

### Stage 1: Free immediate cancellation

Before vendor acceptance, customer cancellation completes immediately.

Allowed immediate-cancel statuses:

- `pending_payment`
- `placed`
- `pending_vendor_acceptance`

Behavior:

- order status becomes `cancelled`
- paid pickup orders may trigger automatic refund processing server-side

### Stage 2: Vendor approval required

After vendor acceptance, pickup cancellation creates a pending request instead of cancelling immediately.

Approval-required statuses:

- `accepted`
- `preparing`
- `ready_for_pickup`

Pending cancellation response:

```json
{
  "message": "Cancellation request submitted and is awaiting vendor approval.",
  "order": {
    "id": "44444444-4444-4444-4444-444444444444",
    "status": "cancellation_requested"
  }
}
```

If a pending request already exists:

```json
{
  "message": "A cancellation request is already pending vendor approval.",
  "order": {
    "id": "44444444-4444-4444-4444-444444444444",
    "status": "cancellation_requested"
  }
}
```

Mobile rules:

- treat `cancellation_requested` as a non-final state
- keep showing the underlying operational order status in tracking UI until vendor/admin decides the request
- refresh order details after submit instead of removing the order from active lists immediately

## UI Guidance

Suggested cancel button behavior:

- trust `can_cancel` from order list/detail responses
- if pickup order is accepted/preparing/ready, show copy that vendor approval may be required
- after submit with `cancellation_requested`, show pending-cancellation banner instead of cancelled state

## Related Error Codes

- `ORDER_CANNOT_BE_CANCELLED`
- `PICKUP_CANCEL_NEEDS_VENDOR_APPROVAL`
- `CANCELLATION_REQUEST_ALREADY_PENDING`

See localized API error messages in `SharedResource` for customer-facing error text.

## Related Endpoints

- `GET /api/orders/{orderId}` for refreshed `can_cancel`
- `GET /api/orders/active` for active-order state after cancellation submit

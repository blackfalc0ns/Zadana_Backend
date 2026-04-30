# Customer Order Support Cases Contract

## Status

- `implemented`

## Purpose

This contract is the source of truth for customer mobile integration for:

- complaints
- return requests
- support case attachments
- support case realtime updates

The backend now uses a unified support-case workflow instead of relying on the legacy complaint-only flow.

## Important Scope Notes

- Mobile should use the new `/api/orders/{orderId}/cases` endpoints for all new complaint and return-request work.
- The legacy `/api/orders/{orderId}/complaints` endpoints still exist for backward compatibility, but they are no longer the preferred integration path.
- Customer mobile currently supports:
  - upload attachment
  - create support case
  - list support cases for an order
  - get support case details
- There is currently no customer API endpoint to submit an evidence reply after admin requests more evidence.
- If admin requests more evidence, mobile should show the latest `customer_visible_note` and guide the user to contact support or wait for the next API revision if a reply action is required.

## Customer Endpoints

### 1. Upload Support Case Attachment

- `POST /api/orders/{orderId}/cases/attachments`

Request type:

- `multipart/form-data`

Form fields:

- `file`: required file upload field

Example response:

```json
{
  "file_name": "invoice-photo.jpg",
  "url": "https://cdn.example.com/orders/support-cases/44444444-4444-4444-4444-444444444444/invoice-photo.jpg"
}
```

Mobile note:

- Upload attachments first
- Then send returned `file_name` and `url` inside the create-case request

### 2. Create Support Case

- `POST /api/orders/{orderId}/cases`

Request body:

```json
{
  "type": "return_request",
  "reason_code": "payment_issue",
  "message": "The delivered order is damaged and I need a return.",
  "attachments": [
    {
      "file_name": "invoice-photo.jpg",
      "file_url": "https://cdn.example.com/orders/support-cases/44444444-4444-4444-4444-444444444444/invoice-photo.jpg"
    }
  ]
}
```

Example response:

```json
{
  "message": "support case submitted successfully",
  "case": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "order_id": "44444444-4444-4444-4444-444444444444",
    "type": "return_request",
    "status": "submitted",
    "queue": "finance",
    "priority": "high",
    "reason_code": "payment_issue",
    "message": "The delivered order is damaged and I need a return.",
    "customer_visible_note": null,
    "decision_notes": null,
    "created_at": "2026-04-30T08:30:00Z",
    "updated_at": "2026-04-30T08:30:00Z",
    "sla_due_at_utc": "2026-05-01T08:30:00Z",
    "requested_refund_amount": 133.5,
    "approved_refund_amount": null,
    "refund_method": null,
    "cost_bearer": null,
    "attachments": [
      {
        "file_name": "invoice-photo.jpg",
        "file_url": "https://cdn.example.com/orders/support-cases/44444444-4444-4444-4444-444444444444/invoice-photo.jpg"
      }
    ],
    "activities": [
      {
        "action": "submitted",
        "title": "Return request submitted",
        "note": "The delivered order is damaged and I need a return.",
        "actor_role": "customer",
        "visible_to_customer": true,
        "created_at": "2026-04-30T08:30:00Z"
      }
    ]
  }
}
```

### 3. Get Support Cases For One Order

- `GET /api/orders/{orderId}/cases`

Example response:

```json
{
  "items": [
    {
      "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "order_id": "44444444-4444-4444-4444-444444444444",
      "type": "complaint",
      "status": "in_review",
      "queue": "support",
      "priority": "medium",
      "reason_code": "delivery_delay",
      "message": "The order arrived much later than expected.",
      "customer_visible_note": "We are reviewing your case.",
      "decision_notes": null,
      "created_at": "2026-04-30T07:15:00Z",
      "updated_at": "2026-04-30T07:25:00Z",
      "sla_due_at_utc": "2026-05-01T07:15:00Z",
      "requested_refund_amount": null,
      "approved_refund_amount": null,
      "refund_method": null,
      "cost_bearer": null,
      "attachments": [],
      "activities": [
        {
          "action": "submitted",
          "title": "Complaint submitted",
          "note": "The order arrived much later than expected.",
          "actor_role": "customer",
          "visible_to_customer": true,
          "created_at": "2026-04-30T07:15:00Z"
        },
        {
          "action": "assigned",
          "title": "Case assigned for review",
          "note": null,
          "actor_role": "admin",
          "visible_to_customer": false,
          "created_at": "2026-04-30T07:20:00Z"
        }
      ]
    }
  ]
}
```

### 4. Get One Support Case

- `GET /api/orders/{orderId}/cases/{caseId}`

Example response:

```json
{
  "case": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "order_id": "44444444-4444-4444-4444-444444444444",
    "type": "complaint",
    "status": "awaiting_customer_evidence",
    "queue": "operations",
    "priority": "medium",
    "reason_code": "delivery_delay",
    "message": "The order arrived much later than expected.",
    "customer_visible_note": "Please upload a photo of the package and the invoice.",
    "decision_notes": "Need more proof before decision.",
    "created_at": "2026-04-30T07:15:00Z",
    "updated_at": "2026-04-30T08:00:00Z",
    "sla_due_at_utc": "2026-05-01T08:00:00Z",
    "requested_refund_amount": null,
    "approved_refund_amount": null,
    "refund_method": null,
    "cost_bearer": null,
    "attachments": [],
    "activities": [
      {
        "action": "submitted",
        "title": "Complaint submitted",
        "note": "The order arrived much later than expected.",
        "actor_role": "customer",
        "visible_to_customer": true,
        "created_at": "2026-04-30T07:15:00Z"
      },
      {
        "action": "request_evidence",
        "title": "Additional evidence requested",
        "note": "Please upload a photo of the package and the invoice.",
        "actor_role": "admin",
        "visible_to_customer": true,
        "created_at": "2026-04-30T08:00:00Z"
      }
    ]
  }
}
```

## `active_case` in Order Detail and Tracking

The backend now returns a short support-case summary in:

- `GET /api/orders/{orderId}`
- `GET /api/orders/{orderId}/tracking`

Response field:

- `active_case`

Example snippet:

```json
{
  "active_case": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "type": "complaint",
    "status": "in_review",
    "queue": "support",
    "priority": "medium",
    "reason_code": "delivery_delay",
    "message": "The order arrived much later than expected.",
    "created_at": "2026-04-30T07:15:00Z",
    "updated_at": "2026-04-30T07:25:00Z"
  }
}
```

Mobile guidance:

- If `active_case` is not null, show the current support-case status on order details and tracking screens.
- Use the `active_case.id` value to open the full support-case details screen.

## Supported Values

### `type`

- `complaint`
- `return_request`

### `status`

- `submitted`
- `in_review`
- `awaiting_customer_evidence`
- `approved`
- `rejected`
- `resolved`

### `queue`

- `support`
- `finance`
- `operations`

### `priority`

- `low`
- `medium`
- `high`
- `critical`

### Known `reason_code` values used by backend routing

- `payment_issue`
- `delivery_delay`
- `prep_delay`
- `fraud`
- `fraud_suspicion`

Important note:

- `reason_code` is currently a string field, but the values above affect queue and priority defaults.
- Mobile should send stable snake_case values.

## Eligibility Rules

### Complaint creation

- complaints are allowed only after the order leaves `pending_payment`

### Return-request creation

- return requests are allowed only when the order is already `delivered`

### One active case rule

- only one active support case can exist on the same order at a time
- if another case is still open, the backend returns `ORDER_SUPPORT_CASE_ALREADY_EXISTS`

## Realtime and Notifications

The backend sends support-case updates through:

- inbox notifications
- SignalR notifications hub
- OneSignal push notifications

### Inbox notification type

- `order_support_case_changed`

### SignalR hub

- route: `/hubs/notifications`

### SignalR event

- `ReceiveOrderSupportCaseChanged`

Payload shape:

```json
{
  "caseId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "orderId": "44444444-4444-4444-4444-444444444444",
  "orderNumber": "ORD-10245",
  "type": "complaint",
  "status": "approved",
  "action": "approved",
  "targetUrl": "/orders/44444444-4444-4444-4444-444444444444/cases/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "changedAtUtc": "2026-04-30T08:45:00Z"
}
```

Recommended mobile behavior:

- if the support-case details screen is open, refresh `GET /api/orders/{orderId}/cases/{caseId}`
- if order details or tracking is open, refresh the order endpoint and reuse `active_case`
- update the in-app notifications badge/list when `ReceiveNotification` is also received

## Suggested Mobile Flow

1. Open order details or tracking and read `active_case`.
2. If the user wants to complain or request a return, upload zero or more files to `/cases/attachments`.
3. Create the support case through `POST /api/orders/{orderId}/cases`.
4. Navigate to a support-case details screen using the returned `case.id`.
5. Listen for `ReceiveOrderSupportCaseChanged` and refresh the case screen on every update.
6. Show `customer_visible_note` and visible `activities` as the customer-facing timeline.

## Error Cases Mobile Should Handle

- `INVALID_SUPPORT_CASE_TYPE`
- `ORDER_COMPLAINT_NOT_ALLOWED`
- `ORDER_RETURN_NOT_ALLOWED`
- `ORDER_SUPPORT_CASE_ALREADY_EXISTS`
- `INVALID_FILE`

## Primary Backend Sources

- `src/Zadana.Api/Modules/Orders/Controllers/OrdersController.cs`
- `src/Zadana.Api/Modules/Orders/Requests/MyOrdersRequests.cs`
- `src/Zadana.Api/Realtime/NotificationHub.cs`
- `src/Zadana.Api/Realtime/Contracts/OrderSupportCaseChangedRealtimePayload.cs`
- `src/Zadana.Application/Modules/Orders/Services/OrderSupportCaseWorkflowService.cs`

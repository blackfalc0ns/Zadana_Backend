# Customer Notifications Contract

## Status

- `implemented`

## Endpoints

- `GET /api/notifications`
- `GET /api/notifications/unread-count`
- `POST /api/notifications/{id}/read`
- `POST /api/notifications/read-all`

## Get Notifications

### Query Parameters

- `page`
- `per_page`
- `type`
- `is_read`
- `from_utc`
- `to_utc`

Example response:

```json
{
  "items": [
    {
      "id": "99999999-9999-9999-9999-999999999999",
      "titleAr": "طلبك خرج للتوصيل",
      "titleEn": "Your order is out for delivery",
      "bodyAr": "شارك رمز التسليم مع المندوب عند الوصول.",
      "bodyEn": "Share the delivery OTP with the driver on arrival.",
      "type": "order-on-the-way",
      "referenceId": "44444444-4444-4444-4444-444444444444",
      "data": "{\"orderNumber\":\"ORD-10245\"}",
      "dataObject": null,
      "isRead": false,
      "createdAtUtc": "2026-04-25T11:40:00Z"
    }
  ],
  "page": 1,
  "perPage": 20,
  "total": 1,
  "unreadCount": 1,
  "hasMore": false
}
```

## Unread Count

- `GET /api/notifications/unread-count`

Example response:

```json
{
  "count": 3
}
```

## Important Notes

- `referenceId` can be used to deep link to order details or order tracking
- The backend returns bilingual title/body fields
- Mobile can choose the localized field locally

## Pickup Notification Types

In addition to generic order status notifications, pickup fulfillment can emit dedicated notification `type` values:

| `type` | When sent | Mobile action |
| --- | --- | --- |
| `pickup_ready` | Vendor marks pickup order ready | Open order details / tracking for OTP and branch info |
| `pickup_reminder` | Pickup window midpoint or near-deadline reminder | Open order details / tracking |
| `pickup_deadline_extended` | Branch was closed and pickup deadline extended | Refresh pickup deadline UI |
| `pickup_expired` | Pickup window expired and order cancelled | Show cancelled state; refresh order details |
| `pickup_otp_regenerated` | Customer requested OTP resend | Refresh order details to load new OTP expiry |

Example pickup-ready item:

```json
{
  "id": "99999999-9999-9999-9999-999999999999",
  "titleAr": "الطلب جاهز للاستلام",
  "titleEn": "Order Ready for Pickup",
  "bodyAr": "طلبك رقم ORD-10246 جاهز للاستلام من الفرع. افتح تفاصيل الطلب لعرض رمز الاستلام.",
  "bodyEn": "Your order #ORD-10246 is ready for pickup at the branch. Open order details to view your pickup code.",
  "type": "pickup_ready",
  "referenceId": "44444444-4444-4444-4444-444444444444",
  "data": "{\"orderId\":\"44444444-4444-4444-4444-444444444444\",\"fulfillmentType\":\"Pickup\",\"eventName\":\"order.pickup.ready\"}",
  "dataObject": null,
  "isRead": false,
  "createdAtUtc": "2026-04-25T12:10:00Z"
}
```

Example pickup reminder item:

```json
{
  "type": "pickup_reminder",
  "referenceId": "44444444-4444-4444-4444-444444444444",
  "data": "orderId=44444444-4444-4444-4444-444444444444;reminderPercent=90"
}
```

Notes:

- pickup OTP code itself is not included in push/inbox notification payloads
- customer must open order details or receive the customer-only realtime payload to view OTP
- `pickup_convert_pay_delta` is not currently emitted as a dedicated notification type; if added later, treat it as a payment-action notification linked to `referenceId`

Filter guidance:

- use `type = pickup_ready` for primary ready-for-pickup inbox grouping
- use `referenceId` for order deep links across all pickup notification types

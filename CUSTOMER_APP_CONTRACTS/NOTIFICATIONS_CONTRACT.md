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

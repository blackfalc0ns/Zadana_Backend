# Driver Notifications Contract

## Status

- `implemented`

## Purpose

هذا الملف يشرح inbox notifications الخاصة بالمندوب، وهي backend-driven الآن وليست static UI.

## Endpoints

### 1. Get Notifications

- `GET /api/drivers/notifications`

query params:

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
      "id": "88888888-8888-8888-8888-888888888888",
      "titleAr": "طلب جديد",
      "titleEn": "New order",
      "bodyAr": "لديك عرض توصيل جديد",
      "bodyEn": "You have a new delivery offer",
      "type": "delivery-offer",
      "referenceId": "11111111-1111-1111-1111-111111111111",
      "data": "{\"target\":\"driver-offer\"}",
      "dataObject": {
        "target": "driver-offer"
      },
      "isRead": false,
      "createdAtUtc": "2026-04-25T10:00:00Z"
    }
  ],
  "page": 1,
  "perPage": 20,
  "total": 1,
  "unreadCount": 1,
  "hasMore": false
}
```

### 2. Get Unread Count

- `GET /api/drivers/notifications/unread-count`

response:

```json
{
  "count": 3
}
```

### 3. Mark Single Notification as Read

- `POST /api/drivers/notifications/{id}/read`

response:

```json
{
  "message": "notification marked as read"
}
```

### 4. Mark All as Read

- `POST /api/drivers/notifications/read-all`

response:

```json
{
  "message": "all notifications marked as read",
  "count": 5
}
```

## Important Notes

- الشاشة يمكنها عرض:
  - `titleAr/titleEn`
  - `bodyAr/bodyEn`
  - `createdAtUtc`
  - `isRead`
- إذا التطبيق يحتاج shape أبسط مثل:
  - `title`
  - `body`
  - `time`
  - `isUnread`
  فهذا يتم في adapter داخل الموبايل
- `unreadCount` هنا يجب أن يبقى متسقًا مع `home.unreadAlerts`

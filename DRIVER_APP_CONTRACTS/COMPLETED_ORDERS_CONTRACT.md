# Driver Completed Orders Contract

## Status

- `implemented`

## Purpose

هذه العقود مخصصة لشاشة `Completed Orders` عند المندوب، وتعمل الآن من الـ backend مباشرة بدل الاعتماد على assignment history الخام أو mock data.

## Endpoints

### 1. Get Completed Orders List

- `GET /api/drivers/orders/completed`

Optional query:

- `status=delivered`
- `status=cancelled`
- `status=deliveryFailed`

Example response:

```json
{
  "items": [
    {
      "id": "22222222-2222-2222-2222-222222222222",
      "merchantName": "Driver Read Vendor",
      "customerName": "Ahmed Customer",
      "completedAtUtc": "2026-04-25T09:15:00Z",
      "status": "delivered",
      "amount": 112.0,
      "distanceKm": 4.6,
      "paymentMethod": "CashOnDelivery",
      "deliveryAddress": "Yasmin District",
      "items": [
        {
          "name": "Fresh Item",
          "quantity": 2,
          "unitPrice": 50.0,
          "lineTotal": 100.0
        }
      ]
    }
  ],
  "totalCount": 1
}
```

### 2. Get Completed Order Detail

- `GET /api/drivers/orders/completed/{orderId}`

Example response:

```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "assignmentId": "11111111-1111-1111-1111-111111111111",
  "orderNumber": "ORD-10025",
  "merchantName": "Driver Read Vendor",
  "merchantPhone": "01000000059",
  "customerName": "Ahmed Customer",
  "customerPhone": "01000000061",
  "pickupAddress": "Olaya Street",
  "deliveryAddress": "Yasmin District",
  "status": "delivered",
  "paymentMethod": "CashOnDelivery",
  "amount": 112.0,
  "deliveryFee": 12.0,
  "distanceKm": 4.6,
  "completedAtUtc": "2026-04-25T09:15:00Z",
  "items": [
    {
      "name": "Fresh Item",
      "quantity": 2,
      "unitPrice": 50.0,
      "lineTotal": 100.0
    }
  ]
}
```

## Returned Status Values

القيم الحالية في completed orders:

- `delivered`
- `cancelled`
- `deliveryFailed`

## Important Notes

- هذه الشاشة لا يجب أن تعتمد على:
  - `GET /api/drivers/assignments/history`
- لأن `assignments/history` ترجع history تشغيلية خام، وليست completed orders UI contract
- القائمة هنا order-level، لا assignment-level
- `distanceKm` تأتي من:
  - `quotedDistanceKm` لو موجودة على الطلب
  - أو fallback حساب تقريبي من الفرع إلى عنوان العميل

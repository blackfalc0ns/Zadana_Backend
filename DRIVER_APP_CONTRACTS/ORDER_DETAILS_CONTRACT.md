# Driver Order Details Contract

## Status

- `implemented`

## Purpose

هذا الملف يشرح كيف يقرأ تطبيق المندوب تفاصيل المهمة الحالية بشكل `assignment-first`.

الـ source of truth في شاشة الطلب الحالية يجب أن يكون:

- `assignmentStatus`
- `driverArrivalState`
- `allowedActions`

وليس local enum داخل التطبيق.

## Main Endpoints

### 1. Get Assignment Detail

- `GET /api/drivers/assignments/{assignmentId}`

يرجع snapshot تشغيلية كاملة للمهمة.

Example response:

```json
{
  "assignmentId": "11111111-1111-1111-1111-111111111111",
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "ORD-10025",
  "assignmentStatus": "Accepted",
  "homeState": "OnMission",
  "allowedActions": [
    "arrived_at_vendor"
  ],
  "vendorName": "Driver Read Vendor",
  "pickupAddress": "Olaya Street",
  "pickupLatitude": 24.7136,
  "pickupLongitude": 46.6753,
  "storePhone": "01000000060",
  "customerName": "Ahmed Customer",
  "deliveryAddress": "Yasmin District",
  "deliveryLatitude": 24.7821,
  "deliveryLongitude": 46.6520,
  "customerPhone": "01000000061",
  "paymentMethod": "CashOnDelivery",
  "codAmount": 60.0,
  "pickupOtpRequired": true,
  "pickupOtpStatus": "pending",
  "deliveryOtpRequired": false,
  "deliveryOtpStatus": "not_required",
  "driverArrivalState": "en_route",
  "orderItems": [
    {
      "name": "Fresh Item",
      "quantity": 2,
      "unitPrice": 50.0,
      "lineTotal": 100.0
    }
  ]
}
```

### 2. Get Current Assignment Envelope

- `GET /api/drivers/assignments/current`

هذا endpoint ما زال موجودًا كـ lightweight check:

- هل يوجد assignment حالية أم لا
- gate status إن كان السائق غير تشغيلي

لكن شاشة الـ detail نفسها يجب أن تبني على:

- `GET /api/drivers/assignments/{assignmentId}`

### 3. Driver Actions

الـ lifecycle التشغيلي يبقى عبر endpoints منفصلة:

- `POST /api/drivers/offers/{assignmentId}/accept`
- `POST /api/drivers/offers/{assignmentId}/reject`
- `POST /api/drivers/orders/{orderId}/arrived-at-vendor`
- `POST /api/drivers/orders/{orderId}/picked-up`
- `POST /api/drivers/orders/{orderId}/on-the-way`
- `POST /api/drivers/orders/{orderId}/arrived-at-customer`
- `POST /api/drivers/orders/{orderId}/delivered`
- `POST /api/drivers/orders/{orderId}/delivery-failed`
- `POST /api/drivers/assignments/{assignmentId}/verify-otp`

## Allowed Actions

القيم الحالية التي قد ترجع في `allowedActions`:

- `accept_offer`
- `reject_offer`
- `arrived_at_vendor`
- `mark_picked_up`
- `mark_on_the_way`
- `arrived_at_customer`
- `verify_delivery_otp`
- `mark_delivered`

## Mapping Rules

الموبايل يجب أن يتصرف بناءً على `allowedActions`، لا على assumptions.

أمثلة:

- لو `allowedActions = ["accept_offer", "reject_offer"]`
  - اعرض offer controls فقط
- لو `allowedActions = ["arrived_at_vendor"]`
  - اعرض CTA الوصول للتاجر
- لو `allowedActions = ["mark_picked_up"]`
  - اعرض CTA الاستلام
- لو `allowedActions = ["verify_delivery_otp"]`
  - اعرض إدخال OTP العميل

## OTP Notes

- pickup OTP verification endpoint:
  - `POST /api/drivers/assignments/{assignmentId}/verify-otp`
  - body:

```json
{
  "otpType": "pickup",
  "otpCode": "1234"
}
```

- delivery OTP verification endpoint:
  - `POST /api/drivers/assignments/{assignmentId}/verify-otp`
  - body:

```json
{
  "otpType": "delivery",
  "otpCode": "5678"
}
```

example success response:

```json
{
  "assignmentId": "11111111-1111-1111-1111-111111111111",
  "orderId": "22222222-2222-2222-2222-222222222222",
  "otpType": "delivery",
  "status": "verified",
  "message": "OTP verified successfully."
}
```

## Important Mobile Notes

- `assignmentId` هو المرجع الأساسي لشاشة المهمة
- لا تعتمد على `orderId` وحده لإدارة accept/reject
- `homeState` قد يكون:
  - `IncomingOffer`
  - `OnMission`
- `pickupOtpRequired` و`deliveryOtpRequired` هما المرجع الرسمي لعرض OTP UI

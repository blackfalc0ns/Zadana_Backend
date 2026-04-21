# Mobile Customer Orders & Payment Integration Guide

هذا الملف مخصص لمبرمج الموبايل لدمج شاشة الطلبات والدفع والإلغاء والحذف بشكل واضح وعملي مع الباك الحالي.

## الهدف

هذا الدليل يغطي:

- عرض طلبات المستخدم
- التعامل مع الطلبات التي لم يكتمل دفعها
- `Retry Payment`
- `Delete Order`
- `Cancel Order`
- `Cancellation Reasons`
- كيف يحدد التطبيق أي زر يظهر للمستخدم

## قاعدة العمل الأساسية

### 1. الطلب الأونلاين لا يظهر للتاجر قبل نجاح الدفع

لو الطلب `Card` ولم يكتمل الدفع:

- يظل الطلب في `PendingPayment`
- لا يظهر للتاجر
- لا يصل للتاجر كـ new order

بعد نجاح الدفع فقط:

- يتحول إلى `PendingVendorAcceptance`
- يبدأ يظهر للتاجر

### 2. عند المستخدم توجد 3 أفعال مختلفة

- `Retry Payment`
  فقط عندما يكون الطلب `Card` وما زال في `PendingPayment`
- `Delete Order`
  فقط عندما يكون الطلب غير مدفوع وما زال في `PendingPayment`
- `Cancel Order`
  فقط بعد خروج الطلب من `PendingPayment` وقبل الوصول إلى `ReadyForPickup`

## Base Rules For Mobile

التطبيق لا يجب أن يستنتج المنطق من الحالة وحدها.

اعتمد على الفلاجس القادمة من الباك:

- `can_retry_payment`
- `can_delete`
- `can_cancel`

### عرض الأزرار

- لو `can_retry_payment = true` اعرض زر `إعادة محاولة الدفع`
- لو `can_delete = true` اعرض زر `حذف الطلب`
- لو `can_cancel = true` اعرض زر `إلغاء الطلب`

إذا كانت القيمة `false` لا تعرض الزر.

## Auth & Headers

كل endpoints الخاصة بالعميل تحتاج:

- `Authorization: Bearer <token>`

وعند إنشاء الطلب أو تأكيد الدفع أو إعادة المحاولة يفضل الاستمرار على:

- `X-Device-Id: <device-id>`

خصوصًا في flows المرتبطة بالكارت وPaymob.

## Main Endpoints

### 1. Get Active Orders

`GET /api/orders/active`

### 2. Get Completed Orders

`GET /api/orders/completed`

### 3. Get Return Orders

`GET /api/orders/returns`

### 4. Get Order Details

`GET /api/orders/{orderId}`

### 5. Retry Payment

`POST /api/orders/{orderId}/retry-payment`

يعيد فتح Paymob session جديدة لنفس الطلب.

### 6. Delete Unpaid Pending Order

`DELETE /api/orders/{orderId}`

يحذف الطلب نهائيًا فقط إذا كان:

- `PendingPayment`
- غير مدفوع
- لم يصل للتاجر

### 7. Cancel Order

`POST /api/orders/{orderId}/cancel`

### 8. Get Cancellation Reasons

`GET /api/orders/cancellation-reasons`

يعيد قائمة الأسباب الجاهزة لاستخدامها في bottom sheet أو modal الإلغاء.

## Order Response Fields Important For Mobile

هذه الحقول مهمة جدًا في:

- `GET /api/orders/active`
- `GET /api/orders/{orderId}`

### Fields

- `status`
- `payment_status`
- `payment_method`
- `can_retry_payment`
- `can_delete`
- `can_cancel`

### Example

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "created_at": "2026-04-21T10:00:00Z",
  "total_price": 125.0,
  "status": "pending",
  "payment_status": "pending",
  "payment_method": "card",
  "can_retry_payment": true,
  "can_delete": true,
  "can_cancel": false,
  "items_count": 2,
  "items": [
    {
      "id": "22222222-2222-2222-2222-222222222222",
      "name": "Brake Pads",
      "quantity": 1,
      "price": 125.0
    }
  ]
}
```

## Meanings Of Order Fields

### `status`

القيمة المعروضة للموبايل هي status مبسط:

- `pending`
- `processing`
- `delivered`
- `returning`
- `cancelled`

### `payment_status`

- `pending`
- `paid`
- `failed`

### `payment_method`

- `card`
- `cash`
- `bank`

## Button Decision Matrix

### Case A: Pending unpaid card order

غالبًا:

- `status = pending`
- `payment_status = pending` أو `failed`
- `payment_method = card`
- `can_retry_payment = true`
- `can_delete = true`
- `can_cancel = false`

الموبايل يعرض:

- `Retry Payment`
- `Delete Order`

ولا يعرض:

- `Cancel Order`

### Case B: Paid order waiting vendor response

غالبًا:

- `status = processing`
- `payment_status = paid`
- `can_retry_payment = false`
- `can_delete = false`
- `can_cancel = true`

الموبايل يعرض:

- `Cancel Order`

### Case C: Order in preparing

- `can_cancel = true`

### Case D: Order reached ready for pickup or later

- `can_cancel = false`

لا تعرض زر الإلغاء.

## Retry Payment Flow

### When To Call

استدعِ:

`POST /api/orders/{orderId}/retry-payment`

فقط إذا:

- `can_retry_payment = true`

### Response Example

```json
{
  "message": "payment retry session created successfully",
  "payment": {
    "id": "33333333-3333-3333-3333-333333333333",
    "provider": "paymob",
    "status": "pending",
    "iframe_url": "https://accept.paymob.com/api/acceptance/iframes/xxx?payment_token=yyy",
    "provider_reference": "provider-ref-123"
  }
}
```

### Mobile Behavior

بعد نجاح الـ endpoint:

1. افتح `iframe_url`
2. أكمل Paymob flow
3. بعد الرجوع من الدفع:
   اعمل refresh للطلب أو لقائمة الطلبات

### Frontend Responsibility In Retry Payment

الفرونت **لا ينشئ** رابط الدفع، ولا يكون مسؤولًا عن تكوين token أو iframe path.

مسؤولية الفرونت هنا فقط:

1. يقرأ `payment.iframe_url` من response
2. يفتح الرابط داخل:
   - `WebView`
   - أو browser/custom tab حسب قرار التطبيق
3. يراقب الرجوع من شاشة الدفع أو success callback/deep link
4. بعد الرجوع يعمل:
   - `GET /api/orders/{orderId}`
   - أو `GET /api/orders/active`
5. يحدّث الـ UI حسب القيم الجديدة القادمة من الباك:
   - `payment_status`
   - `can_retry_payment`
   - `can_delete`
   - `can_cancel`

### What Frontend Should Not Do

- لا ينشئ payment link محليًا
- لا يخزّن منطق تكوين Paymob URL داخل التطبيق
- لا يفترض نجاح الدفع بمجرد فتح `iframe_url`
- لا يغير حالة الطلب محليًا بدون refresh من الباك

### Recommended Mobile UX

- عند الضغط على `Retry Payment`:
  - اعرض loading
  - استدعِ `POST /api/orders/{orderId}/retry-payment`
  - لو رجع `iframe_url` افتح شاشة الدفع مباشرة
- لو فشل الـ endpoint:
  - اعرض رسالة الخطأ القادمة من الباك
- بعد إغلاق شاشة الدفع أو الرجوع منها:
  - اعرض loading قصير
  - أعد جلب الطلب
  - لو `payment_status = paid` أخفِ `Retry Payment` و`Delete Order`
  - لو `payment_status = failed` و`can_retry_payment = true` أظهر `Retry Payment` مرة أخرى

### Important

هذا الـ retry:

- لا ينشئ order جديد
- يعمل على نفس `orderId`

## Delete Order Flow

### When To Call

استدعِ:

`DELETE /api/orders/{orderId}`

فقط إذا:

- `can_delete = true`

### Response Example

```json
{
  "message": "order deleted successfully",
  "order_id": "11111111-1111-1111-1111-111111111111",
  "deleted": true
}
```

### Mobile Behavior

بعد النجاح:

- احذف الطلب من القائمة المحلية
- أو اعمل refetch لقائمة `active`

## Cancel Order Flow

### Step 1: Load reasons

استدعِ:

`GET /api/orders/cancellation-reasons`

### Response Example

```json
[
  {
    "code": "changed_my_mind",
    "label_ar": "غيرت رأيي",
    "label_en": "Changed my mind",
    "requires_note": false
  },
  {
    "code": "ordered_by_mistake",
    "label_ar": "طلبت بالخطأ",
    "label_en": "Ordered by mistake",
    "requires_note": false
  },
  {
    "code": "price_too_high",
    "label_ar": "السعر مرتفع",
    "label_en": "Price is too high",
    "requires_note": false
  },
  {
    "code": "want_to_modify_order",
    "label_ar": "أريد تعديل الطلب",
    "label_en": "I want to modify the order",
    "requires_note": false
  },
  {
    "code": "address_not_suitable",
    "label_ar": "العنوان غير مناسب",
    "label_en": "Address is not suitable",
    "requires_note": false
  },
  {
    "code": "other",
    "label_ar": "أخرى",
    "label_en": "Other",
    "requires_note": true
  }
]
```

### Step 2: Submit cancel

استدعِ:

`POST /api/orders/{orderId}/cancel`

### Request Example With Predefined Reason

```json
{
  "reason_code": "changed_my_mind",
  "note": null
}
```

### Request Example With Other

```json
{
  "reason_code": "other",
  "note": "أحتاج تغيير المدينة والعنوان بالكامل"
}
```

### Backward Compatibility

الباك ما زال يقبل `reason` النصي كحل fallback:

```json
{
  "reason": "Custom reason from legacy client",
  "note": null
}
```

لكن للموبايل الجديد يفضل دائمًا استخدام:

- `reason_code`
- `note`

### Response Example

```json
{
  "message": "order cancelled successfully",
  "order": {
    "id": "11111111-1111-1111-1111-111111111111",
    "status": "cancelled"
  }
}
```

## Validation Rules For Cancel

### Allowed

الإلغاء مسموح فقط عندما:

- `can_cancel = true`

عمليًا قبل الوصول إلى:

- `ReadyForPickup`

### Not Allowed

إذا الطلب وصل إلى:

- `ReadyForPickup`
- `DriverAssigned`
- `PickedUp`
- `OnTheWay`
- `Delivered`

فلا تعرض زر الإلغاء.

### For `other`

إذا:

- `reason_code = other`

فيجب إرسال:

- `note`

ولو لم يتم إرسالها، سيُرفض الطلب من الباك.

## Suggested Mobile UX

### In orders list

- اعرض CTA حسب الفلاجس
- لا تعتمد فقط على `status`

### In order details

- لو `payment_status = failed` و`can_retry_payment = true`
  اعرض banner أو card فيها:
  - `فشل الدفع`
  - `إعادة المحاولة`
  - `حذف الطلب`

### In cancel modal / bottom sheet

- حمّل الأسباب من `cancellation-reasons`
- اعرض radio list
- لو `requires_note = true`
  فعّل textarea وأجعلها required

## Expected Common Errors

### Retry payment

- `ORDER_PAYMENT_RETRY_NOT_ALLOWED`
- `ORDER_ALREADY_PAID`
- `PAYMENT_UNAVAILABLE`

### Delete order

- `ORDER_DELETE_NOT_ALLOWED`
- `ORDER_ALREADY_PAID`
- `ORDER_NOT_PENDING_PAYMENT`

### Cancel order

- `ORDER_CANNOT_BE_CANCELLED`

## Recommended Client-Side Handling

### On `ORDER_PAYMENT_RETRY_NOT_ALLOWED`

- اعمل refresh للطلب
- أخفِ زر retry

### On `ORDER_DELETE_NOT_ALLOWED`

- اعمل refresh للقائمة
- أخفِ زر delete

### On `ORDER_CANNOT_BE_CANCELLED`

- اعمل refresh للطلب
- أخفِ زر cancel

## Real Integration Sequence

### Online card order scenario

1. المستخدم يعمل `Place Order`
2. الباك ينشئ order في `PendingPayment`
3. الموبايل يفتح `payment.iframe_url`
4. لو الدفع فشل أو خرج المستخدم:
   - الطلب يظل عند المستخدم
   - يظهر `Retry Payment`
   - يظهر `Delete Order`
   - لا يظهر للتاجر
5. لو retry نجح:
   - الطلب يتحول إلى `PendingVendorAcceptance`
   - يبدأ يظهر للتاجر
   - يختفي `Retry/Delete`
   - قد يظهر `Cancel Order` إذا كان ما زال قبل `ReadyForPickup`

## Endpoints Summary

### Customer Orders

- `GET /api/orders/active`
- `GET /api/orders/completed`
- `GET /api/orders/returns`
- `GET /api/orders/{orderId}`
- `GET /api/orders/{orderId}/tracking`
- `GET /api/orders/cancellation-reasons`
- `POST /api/orders/{orderId}/cancel`
- `POST /api/orders/{orderId}/retry-payment`
- `DELETE /api/orders/{orderId}`

## Implementation Notes For Mobile Developer

- اعتمد على server flags
- لا تعمل hardcode لمنطق الأزرار
- استخدم `reason_code` في الإلغاء
- استخدم `note` فقط عند الحاجة أو عند `other`
- بعد أي action:
  - اعمل refresh للطلب
  - أو refetch للقائمة

## Reference Files

- [OrdersController.cs](/d:/fullstack%20project/Zadana/Zadana-Backend/src/Zadana.Api/Modules/Orders/Controllers/OrdersController.cs)
- [MyOrdersRequests.cs](/d:/fullstack%20project/Zadana/Zadana-Backend/src/Zadana.Api/Modules/Orders/Requests/MyOrdersRequests.cs)
- [OrderReadService.cs](/d:/fullstack%20project/Zadana/Zadana-Backend/src/Zadana.Infrastructure/Modules/Orders/Services/OrderReadService.cs)
- [CancelCustomerOrderCommand.cs](/d:/fullstack%20project/Zadana/Zadana-Backend/src/Zadana.Application/Modules/Orders/Commands/CancelCustomerOrder/CancelCustomerOrderCommand.cs)
- [RetryPaymobPaymentCommand.cs](/d:/fullstack%20project/Zadana/Zadana-Backend/src/Zadana.Application/Modules/Payments/Commands/RetryPaymobPayment/RetryPaymobPaymentCommand.cs)
- [DeleteCustomerOrderCommand.cs](/d:/fullstack%20project/Zadana/Zadana-Backend/src/Zadana.Application/Modules/Orders/Commands/DeleteCustomerOrder/DeleteCustomerOrderCommand.cs)

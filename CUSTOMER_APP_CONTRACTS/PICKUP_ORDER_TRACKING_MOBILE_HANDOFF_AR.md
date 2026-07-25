# تتبع طلب الاستلام من الفرع — هاندوف الموبايل (تحديث)

تاريخ التحديث: 2026-07-25  
الحالة: مطبّق في الباك إند  
الجمهور: مبرمج تطبيق العميل  
النطاق: **تعديل تتبع الطلب لوضع الاستلام فقط**

Endpoint:

```http
GET /api/orders/{orderId}/tracking
```

---

## 1) ماذا تغيّر؟

لطلبات `fulfillment_type = "pickup"`:

1. التايملاين بقى مسار استلام (مش توصيل).
2. مفيش `out_for_delivery`.
3. `estimated_delivery` = `null`.
4. `driver` / `assigned_driver` = `null`.
5. `fulfillment_type` بقى lowercase: `"pickup"`.
6. حالة `ready_for_pickup` تظهر صراحة (مش بتتهرس إلى `preparing`).

---

## 2) Response مثال (جاري التجهيز)

```json
{
  "order": {
    "id": "9618cf89-f327-4917-bd16-3d24edb2a553",
    "order_number": "ORD-20260725-F24896F1",
    "status": "preparing"
  },
  "fulfillment_type": "pickup",
  "estimated_delivery": null,
  "driver": null,
  "assigned_driver": null,
  "driver_arrival_state": "none",
  "driver_arrival_updated_at_utc": null,
  "delivery_otp": null,
  "show_delivery_otp": false,
  "pickup_otp_code": null,
  "pickup_otp_expires_at_utc": null,
  "pickup_no_show_deadline_utc": "2026-07-26T20:21:14.249+03:00",
  "pickup_branch": {
    "name": "بقالة الأمل -2",
    "address": "مركز المدينة, DHAHRAN, EASTERN",
    "hours_today": "09:00 - 22:00"
  },
  "timeline": [
    { "id": "order_placed", "title": "تم إنشاء الطلب", "time": "08:20 PM", "is_active": false, "is_completed": true },
    { "id": "vendor_confirmed", "title": "أكد المتجر الطلب", "time": "08:21 PM", "is_active": false, "is_completed": true },
    { "id": "preparing", "title": "جاري تجهيز الطلب", "time": "08:21 PM", "is_active": true, "is_completed": false },
    { "id": "ready_for_pickup", "title": "جاهز للاستلام من الفرع", "time": "", "is_active": false, "is_completed": false },
    { "id": "delivered", "title": "تم الاستلام", "time": "", "is_active": false, "is_completed": false }
  ]
}
```

---

## 3) Timeline الاستلام

| id | متى يبقى active |
|---|---|
| `order_placed` | أول ما يتسجل الطلب |
| `vendor_confirmed` | قبول المتجر |
| `preparing` | التجهيز |
| `ready_for_pickup` | جاهز + يظهر OTP |
| `delivered` | بعد تحقق OTP عند التاجر (تم الاستلام) |
| `cancelled` | بدل الخطوة الأخيرة لو الطلب اتلغى |

**مهم:** لو `fulfillment_type === "pickup"` لا تعرض خطوات التوصيل (`out_for_delivery` / سائق / خريطة).

---

## 4) حالات `order.status` في الاستلام

| status | الواجهة |
|---|---|
| `pending` | بانتظار القبول/الدفع |
| `accepted` | تم القبول |
| `preparing` | جاري التجهيز |
| `ready_for_pickup` | جاهز للاستلام — اعرض OTP |
| `delivered` | تم الاستلام |
| `cancelled` | ملغي |

OTP يظهر فقط لما:

- `pickup_otp_code != null`
- عادة مع `status = ready_for_pickup`

---

## 5) قواعد UI

اعرض:

- بطاقة الفرع من `pickup_branch` (اسم + عنوان + ساعات)
- التايملاين أعلاه
- OTP + مهلة `pickup_no_show_deadline_utc` عند الجاهزية

اخفِ تمامًا:

- `estimated_delivery`
- `driver` / `assigned_driver`
- `delivery_otp` / `show_delivery_otp`
- أي UI توصيل/مندوب

```text
if (fulfillment_type == "pickup") {
  show PickupTrackingUI(branch, otp, timeline)
} else {
  show DeliveryTrackingUI(driver, eta, timeline)
}
```

---

## 6) Checklist

- [ ] فرّق UI الاستلام عن التوصيل بـ `fulfillment_type`
- [ ] استخدم timeline الجديد (`ready_for_pickup` بدل `out_for_delivery`)
- [ ] لا تعتمد على `estimated_delivery` في الاستلام
- [ ] اعرض OTP فقط لو `pickup_otp_code != null`
- [ ] اعرض عنوان الفرع من `pickup_branch.address`

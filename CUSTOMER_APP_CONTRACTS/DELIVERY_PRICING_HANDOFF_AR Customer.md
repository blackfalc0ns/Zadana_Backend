# ربط التسعير الجديد - تطبيق العميل

## الحالة

- `implemented`

## الهدف

هذا الملف مخصص لمبرمج تطبيق العميل فقط، ويوضح ما الذي تغير بعد نظام تسعير التوصيل الجديد وكيف يتم ربطه داخل:

- شاشة الـ checkout
- شاشة مراجعة الطلب قبل الدفع
- أي مكان يعرض رسوم التوصيل

## الفكرة العامة

التسعير لم يعد يعتمد فقط على `التاجر -> العميل`.

الآن الباك إند يحسب:

- `driver -> vendor`
- `vendor -> customer`

لكن في تطبيق العميل:

- اعرض `إجمالي رسوم التوصيل` فقط كقيمة أساسية
- لا تحسب أي شيء على الموبايل
- لا تعيد بناء التسعير من الـ coordinates
- اعتبر الباك إند هو مصدر الحقيقة الوحيد

## أهم Endpoint

### 1. Checkout Summary

- `GET /api/checkout/summary?vendor_id={vendorId}&address_id={addressId}&delivery_slot_id={slotId}`

الـ response الآن يحتوي على الحقول المهمة التالية:

- `delivery_breakdown`
- `pricing_mode`
- `used_estimated_driver_pricing`
- `summary.shipping_cost`

## مثال Response

```json
{
  "delivery_breakdown": {
    "driver_to_vendor": {
      "distance_km": 3.2,
      "fee": 9.0
    },
    "vendor_to_customer": {
      "distance_km": 6.8,
      "fee": 14.0
    },
    "total_delivery": 23.0,
    "pricing_mode": "live",
    "used_estimated_driver_pricing": false
  },
  "pricing_mode": "live",
  "summary": {
    "subtotal": 120.0,
    "shipping_cost": 23.0,
    "discount": 0.0,
    "total": 143.0,
    "currency": "SAR"
  }
}
```

## المطلوب في تطبيق العميل

### اعرض هذه القيم

- استخدم `summary.shipping_cost` كقيمة رسوم التوصيل الأساسية في الـ UI
- يمكن استخدام `delivery_breakdown.total_delivery` للمراجعة أو fallback داخليًا
- اعرض `summary.total` كالإجمالي النهائي

### لا تعرض هذه التفاصيل للعميل بشكل افتراضي

- `driver_to_vendor`
- `vendor_to_customer`

هذه موجودة لخدمة الباك إند والدعم والإدارة، لكن UI العميل المفترض حاليًا يعرض `رسوم التوصيل الإجمالية` فقط.

## معنى pricing_mode

- `live`
  - التسعير تم باستخدام موقع مندوب متاح فعليًا
- `estimated`
  - لم يوجد مندوب live مناسب وقت الحساب، فتم استخدام fallback جغرافي

## المطلوب من الـ UI بخصوص pricing_mode

- لا تمنع الطلب إذا كانت القيمة `estimated`
- لا تغير الحساب محليًا
- يمكن فقط عرض ملاحظة خفيفة اختيارية مثل:
  - `تم احتساب رسوم التوصيل حسب أفضل تقدير متاح`

لكن هذا اختياري، وليس مطلوبًا لإتمام الربط.

## مهم جدًا

- لا تستخدم المسافة لحساب الرسوم على الجهاز
- لا تجمع `driver_to_vendor.fee + vendor_to_customer.fee` وتبني عليها totals من عندك
- لا تعتمد على أي logic قديم مثل:
  - `base fee + per km` محليًا
- بعد أي تغيير في:
  - العنوان
  - التاجر
  - الـ delivery slot
  - طريقة الدفع
  يجب إعادة طلب checkout summary من الباك إند

## Order Details بعد إنشاء الطلب

حاليًا شاشة تفاصيل الطلب للعميل ما زالت تعتمد على:

- `GET /api/orders/{orderId}`

واستخدم:

- `summary.shipping_cost`

كقيمة رسوم التوصيل المعروضة للعميل.

لا يوجد مطلوب حاليًا لعرض الـ legs بشكل منفصل داخل order details للعميل.

## Mapping سريع للموبايل

### Checkout screen

- رسوم التوصيل: `summary.shipping_cost`
- الإجمالي النهائي: `summary.total`
- لو احتجت debug داخلي: `delivery_breakdown`

### Order details screen

- رسوم التوصيل: `summary.shipping_cost`

## لو موجود كود قديم

لو عندكم أي كود قديم في الموبايل يحسب رسوم التوصيل من:

- المسافة
- المدينة
- المنطقة
- رسوم ثابتة

يجب تجاهله في شاشة الـ checkout الحالية، لأن الحساب الرسمي أصبح من الباك إند.


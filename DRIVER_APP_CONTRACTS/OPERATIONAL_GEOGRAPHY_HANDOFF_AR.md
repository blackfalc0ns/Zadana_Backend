# تغييرات الموبايل - نطاق التشغيل الجغرافي للمندوب

> **تحديث 2026-08-21:** المصدر الحالي الكامل:  
> [`EASTERN_PROXIMITY_DISPATCH_HANDOFF_AR.md`](./EASTERN_PROXIMITY_DISPATCH_HANDOFF_AR.md)  
> (منطقة شرقية فقط، بدون مدينة، توزيع GPS ≤ 50 كم)

## الحالة

- `implemented`
- تاريخ التحديث الأصلي: 2026-07-09
- تاريخ الاستبدال: 2026-08-21

## الخلاصة

- المنطقة التشغيلية للمندوب: `EASTERN` فقط (المنطقة الشرقية).
- **المدينة لا تُطلب في UI** — `city` اختياري أو `null`، والتوزيع لا يعتمد عليها.
- عناوين العملاء في تطبيق العميل **لا تُقفل** على الشرقية.

## المطلوب من تطبيق المندوب

راجع الـ checklist الكامل في **`EASTERN_PROXIMITY_DISPATCH_HANDOFF_AR.md`**.

باختصار:

```json
{ "region": "EASTERN" }
```

- أخفِ المدن و`primaryZoneId`
- أرسل GPS دوريًا أثناء الأونلاين
- العروض ضمن ≤ 50 كم من الاستلام + GPS طازج (~5 دقائق)

## APIs

- تثبيت المنطقة محليًا أو `GET /api/geography/regions` / `GET /api/geography/driver/regions` ثم فلترة `EASTERN`
- `PUT /api/drivers/me/profile/vehicle` مع `region` بدون مدينة
- مدن التسجيل القديمة `GET /api/geography/driver/regions/EASTERN/cities` **لم تعد مطلوبة للـ UX الجديد**

## ما الذي لا يتغير؟

- تتبع الطلب / تفاصيل المهمة
- قبول ورفض العروض (مع إعادة فحص القرب على السيرفر)
- محفظة المندوب

## Backend branch

`feature/eastern-proximity-dispatch`

# مشكلة تطبيق المندوب — الموقع مش بيتبعت، فالعروض مش بتظهر

**الحالة:** باگ مكسور في تطبيق المندوب (Flutter) — **الباك سليم**  
**تاريخ:** 2026-08-22  
**الجمهور:** فريق تطبيق المندوب (`blackfalc0ns/zadana_delivery` — فرع `development`)  
**الريبو:** لا تُعدَّل ملفات التطبيق من الباك. الملف ده تشخيص + المطلوب فقط.  
**مرتبط بـ:** `EASTERN_PROXIMITY_DISPATCH_HANDOFF_AR.md` §2-ب و `HOME_CONTRACT.md` (`POST /api/drivers/location`)

---

## 1) العرض اللي شوفناه

المندوب يفتح التطبيق → يتسجل أونلاين (`IsAvailable = true`) → طلب توصيل جاهز في الدمام → **مفيش عرض يظهر**.

ده مش عطل توزيع في الباك، ومش لأن الفرع بعيد. السبب إن السيرفر **ما عندوش GPS طازج** للمندوب.

الباك **ما بيتبعش** المندوب لوحده. مفيش GPS من السيرفر. التطبيق هو اللي لازم يبعت الموقع.

---

## 2) دليل من الإنتاج (2026-08-22)

على `ZadanaImportedDb` المناديب الأونلاين كان آخر GPS عندهم:

| اللي اتشاف | المعنى |
|---|---|
| `RecordedAtUtc` عمره **17–18 يوم** | أقدم بكتير من حد الطزاجة (**5 دقايق**) |
| إحداثيات حوالي **30.04 / 31.23** (القاهرة) | مش الدمام/الخبر — أبعد من **50 كم** عن فرع الاستلام |
| `IsAvailable = 1` و`VerificationStatus = Approved` | المندوب «أونلاين» ومعتمد، بس **مش مؤهل للتوزيع** |

النتيجة: `DeliveryPickupAreaMatcher.DriverMatchesPickup` بيرجع `false` لأن `gpsFresh == false` **أو** المسافة > 50 كم. الطلب ما بيتوزعش، والعرض ما بيوصلش للتطبيق.

تعديل GPS يدوي في الداتابيز حل مؤقت للاختبار فقط. أول ما الـ 5 دقايق تعدي من غير `POST` جديد، المندوب يختفي تاني من التوزيع.

---

## 3) عقد الباك (ما بيتغيرش)

### الشرط اللي يخلّي المندوب يدخل التوزيع

```
Approved + Active + IsAvailable
+ region = EASTERN
+ آخر GPS خلال 5 دقايق (DeliveryProximityLimits.GpsFreshnessThreshold)
+ المسافة Haversine لنقطة الاستلام (إحداثيات الفرع) ≤ 50 كم
+ مش مشغول بمهمة تانية
→ يقدر يستلم عرض
```

التوزيع حلقات متوسعة: `5 → 10 → 12 → 15 → 20 → 25 → 30 → 35 → 40 → 45 → 50` كم.  
قبول العرض (`POST /api/drivers/offers/{id}/accept`) يعيد نفس فحص القرب على GPS **حي**.

### الـ endpoint

```http
POST /api/drivers/location
Authorization: Bearer <access_token>
Content-Type: application/json

{
  "latitude": 26.3927,
  "longitude": 50.1135,
  "accuracyMeters": 12
}
```

النجاح: `{ "message": "Location updated" }` (أو المكافئ المعرّب في الـ envelope).

الباك يخزّن:

- صف جديد في `DriverLocations` (سجل)
- أحدث حالة في `DriverLatestLocations`
- `RecordedAtUtc` = وقت السيرفر (UTC) — **متبعتش** timestamp من الموبايل

`EndPoints.driverLocation` في التطبيق أصلاً صحيح: `/drivers/location`.

### امتى لازم الإرسال

| حالة المندوب | مطلوب |
|---|---|
| أونلاين / متاح **من غير مهمة** | إرسال دوري (مستحسن **15–30 ثانية**، أقصى مقبول أقل من 5 دقايق بهامش) |
| مهمة نشطة (Accepted → ArrivedAtVendor → OnTheWay → ArrivedAtCustomer) | يستمر الإرسال (5–10 ثواني كما هو في كود التتبع) |
| أوفلاين | يوقف الإرسال |
| التطبيق في الخلفية وهو أونلاين أو في رحلة | يستمر الإرسال (إذن موقع «طوال الوقت») |

**مهم:** قراءة GPS وعرضه على الخريطة **مش كفاية**. التوزيع بيقرأ جداول الموقع في الداتابيز فقط.

---

## 4) جذر العطل في التطبيق (فرع `development`)

الكود موجود، بس مسار «متاح وبستنى عرض» **ما بيوصلش** للـ API.

### أ) `DriverTrackingCubit` مش بيتنشأ أصلًا

- التسجيل في DI: `factory` مش `lazySingleton`  
  (`lib/core/di/di.config.dart`)
- `DriverHomeScreen` بيعمل `getIt<DriverHomeCubit>()` فقط — **ما فيش** `DriverTrackingCubit`
- `main.dart` ما بيبدأش التتبع
- تایمر الـ 30 ثانية (`_syncAvailabilityLocationPush`) عايش جوّه `DriverTrackingCubit` → **ما بيشتغلش**

الملف: `lib/features/driver_tracking/presentation/manager/driver_tracking_cubit.dart`

### ب) الأونلاين بيطلب أذونات وما بيبعتش موقع

`DriverHomeCubit._toggleAvailability(true)`:

- يطلب foreground + background location
- يضرب `PUT /api/drivers/me/availability`
- **مافيش** `POST /api/drivers/location`

الملف: `lib/features/driver_home/presentation/manager/driver_home_cubit.dart`

الخريطة بتاخد GPS محلي (`_loadCurrentLocation`) وبتستخدم fallback الرياض `24.7136, 46.6753`. ده للعرض فقط.

### ج) `pushDriverLocation()` بيبعت أمر لخدمة مش شغّالة ومفيش فيها إحداثيات

المسار كله:

```
PushDriverLocationUseCase
  → DriverTrackingRemoteDataSourceImpl.pushDriverLocation()
    → _backgroundService.invoke('pushLocation')
```

هاندلر الخدمة:

```dart
service.on('pushLocation').listen((_) async {
  final position = latestEligiblePosition;
  if (position == null) return; // يخرج صامت
  await pushPosition(position, force: true);
});
```

`latestEligiblePosition` بيتملّي فقط بعد `startTracking` → `restartLocationStream`.  
`startTracking` بيشتغل **لما يكون فيه assignment**.  
الخدمة نفسها `autoStart: false` وبتبدأ في `startTracking` فقط.  
من غير مهمة: `invoke('pushLocation')` إما ما يوصلش لعملية حيّة، أو يلاقي `position == null` ويرجع.

نفس الاستدعاء الفاضي بيحصل قبل قبول العرض:

```dart
await _pushDriverLocationUseCase.call(); // بيتفشل بصمت، والقبول يكمل
```

فحتى لو عرض وصل بمعجزة، القبول ممكن يترفض من الباك بسبب GPS قديم/بعيد.

### د) حلقة مفرغة

```
عشان يستلم عرض  ←  محتاج GPS طازج ≤ 5 دقايق وضمن 50 كم
عشان يبعت GPS    ←  الكود الحالي بيستنى مهمة نشطة
عشان يجيب مهمة   ←  محتاج يستلم عرض
```

التتبع أثناء الرحلة (لو اشتغل) **ما يحلّش** استلام العروض.

### هـ) `main` فاضي تقريبًا

فرع `main` مفيهوش `lib/features`. الشغل على `development`.

---

## 5) المطلوب من الموبايل (Checklist)

**متلمسش عقد الـ API. أصلّح مسار الإرسال.**

- [ ] أول ما المندوب يبقى **متاح** (بعد نجاح `availability`): اقرأ GPS الحالي وابعت `POST /api/drivers/location` فورًا. فشل الإرسال = المندوب **مش** أونلاين فعليًا للتوزيع (أظهر خطأ واضح).
- [ ] طول ما هو متاح **ومن غير مهمة**: Timer أو stream يبعت الموقع كل **15–30 ثانية** (وأسرع لو تحرك > ~50–100 م).
- [ ] مسار الـ idle **ما يعتمدش** على `latestEligiblePosition` جوّه خدمة الخلفية وهي واقفة. اقرأ `Geolocator.getCurrentPosition` في isolate التطبيق (أو ابدأ الخدمة من غير `orderId`)، بعدين `POST`.
- [ ] اربط دورة حياة التتبع بـ `DriverHomeCubit` / شاشة الهوم / خدمة runtime — مش `factory` معزول محدش بيطلبه. `DriverTrackingCubit` يا يبقى singleton يبدأ مع جلسة المندوب، يا تتنقل مسؤوليته للهوم.
- [ ] بعد القبول: فضّل التتبع الحالي (5 ث فورم / 10 ث باك) — بس أصلّح الإرسال **قبل** العرض، مش بعده بس.
- [ ] الخلفية: وهو أونلاين أو في رحلة، الإرسال يفضل شغال (إذن «طوال الوقت» + foreground service لو Android يطلب).
- [ ] أوفلاين / logout: وقف الإرسال ووقف الخدمة.
- [ ] لو GPS قديم أو الإذن اترفض: UI واضح («الموقع مش متحدث — مش هتوصلك عروض») بدل شاشة فاضية.
- [ ] المحاكي: عنوان الدمام (مثال `26.3927, 50.1135`) + التأكد من Network log إن `POST /api/drivers/location` بيتكرر وهو أونلاين. تغيير موقع المحاكي من غير POST للسيرفر **ما بيغيّرش** التوزيع.
- [ ] متبعتش إحداثيات القاهرة/الرياض كـ default للسيرفر. الـ fallback على الخريطة للعرض فقط.

---

## 6) اختبار قبول (يدوي)

1. امسح آخر موقع للمندوب أو استنى > 5 دقايق من غير POST.
2. افتح التطبيق → أونلاين في الدمام (محاكي أو جهاز).
3. خلال **أقل من دقيقة**: لازم يظهر صف جديد في `DriverLocations` / يتحدث `DriverLatestLocations` بإحداثيات شرقية عمرها ثواني.
4. التاجر يعلّم طلب **Delivery** كـ `ReadyForPickup`.
5. العرض لازم يظهر للمندوب القريب.
6. وقف التحديث > 5 دقايق → العروض الجديدة تتوقف / القبول يفشل.
7. ارجع حدّث الموقع → يرجع يدخل التوزيع.
8. اقبل عرض → التتبع يفضل شغال أثناء المهمة وبعد الخلفية.
9. أوفلاين → الإرسال يقف.

تحقق سريع من الداتابيز (من غير أسرار في الشات):

```sql
SELECT d.Id, d.IsAvailable, d.VerificationStatus, d.Region,
       l.Latitude, l.Longitude, l.RecordedAtUtc,
       DATEDIFF(MINUTE, l.RecordedAtUtc, GETUTCDATE()) AS GpsAgeMin
FROM Drivers d
LEFT JOIN DriverLatestLocations l ON l.DriverId = d.Id
WHERE d.IsAvailable = 1;
```

`GpsAgeMin` لازم يكون **< 5** وهو أونلاين. الإحداثيات لازم تبقى شرقية (حوالي `26.2–26.5` / `49.9–50.3`) مش القاهرة.

---

## 7) خارج السكوب

- تغيير حد الـ 5 دقايق أو حلقات التوزيع في الباك.
- تعديل `zadana_delivery` من مسار الباك (التعديل عند فريق الموبايل على فرع `development`).
- تحديث يدوي لصفوف GPS في الإنتاج كحل دائم.

---

## 8) مراجع باك

- `src/Zadana.Api/Modules/Delivery/Controllers/DriversController.cs` — `POST location`
- `src/Zadana.Application/Modules/Delivery/Support/DeliveryProximityLimits.cs`
- `src/Zadana.Application/Modules/Delivery/Support/DeliveryPickupAreaMatcher.cs`
- `src/Zadana.Infrastructure/Modules/Delivery/Services/DeliveryDispatchService.cs`
- `DRIVER_APP_CONTRACTS/HOME_CONTRACT.md` — `POST /api/drivers/location`
- `DRIVER_APP_CONTRACTS/EASTERN_PROXIMITY_DISPATCH_HANDOFF_AR.md`

# Driver Documents Compliance Expansion Handoff

## Status

- `implemented`

## Purpose

هذا الملف مخصص لمبرمج الموبايل لشرح التعديلات الجديدة الخاصة بملف المندوب والمستندات والاشعارات بعد توسعة:

- البطاقة الشخصية
- رخصة القيادة
- رخصة المركبة
- تواريخ انتهاء المستندات
- حالة مراجعة كل مستند بشكل منفصل
- اشعارات القبول والرفض وطلب الاستكمال

الملف ده يعتبر handoff سريع ومفصل للتنفيذ على تطبيق الموبايل.

## Main Idea

المندوب لازم يرسل 3 document packets منفصلين:

1. `National ID`
2. `Driver License`
3. `Vehicle License`

وكل packet له:

- رقم مستند
- تاريخ انتهاء
- صورة أو صور
- حالة مراجعة مستقلة من الأدمن

## Required Mobile Screens Update

الموبايل لازم يقسم شاشة onboarding/profile إلى الأقسام التالية:

1. بيانات شخصية
2. بيانات المركبة والمستندات النصية
3. رفع الصور
4. شاشة حالة المراجعة والاشعارات

## New Form Fields

### 1. National ID Section

- `nationalId`
- `nationalIdExpiryDate`
- `nationalIdFrontImageUrl`
- `nationalIdBackImageUrl`

### 2. Driver License Section

- `licenseNumber`
- `driverLicenseExpiryDate`
- `licenseImageUrl`

### 3. Vehicle License Section

- `vehicleLicenseNumber`
- `vehicleLicenseExpiryDate`
- `vehicleImageUrl`

### 4. Still Required

- `vehicleType`
- `personalPhotoUrl`
- `address`
- `region`
- `city`

## Upload Rule

رفع الصور ما زال بنفس الأسلوب الحالي:

1. ارفع الملف أولًا عن طريق:
   - `POST /api/files/upload`
2. خذ الـ URL الناتج
3. مرر الـ URL داخل endpoint المناسب

الموبايل لا يرسل binary image داخل profile endpoints.

## Endpoints The Mobile App Must Use

### 1. Register Driver

- `POST /api/drivers/register`

Example body:

```json
{
  "fullName": "Ahmed Driver",
  "email": "driver@example.com",
  "phone": "01000000000",
  "password": "P@ssw0rd",
  "vehicleType": "Motorcycle",
  "nationalId": "29901011234567",
  "licenseNumber": "DL-12345",
  "nationalIdExpiryDate": "2028-05-01T00:00:00Z",
  "driverLicenseExpiryDate": "2027-10-01T00:00:00Z",
  "vehicleLicenseNumber": "VL-99881",
  "vehicleLicenseExpiryDate": "2027-12-31T00:00:00Z",
  "address": "Al Khobar",
  "region": "EASTERN",
  "city": null,
  "nationalIdFrontImageUrl": "https://cdn/id-front.jpg",
  "nationalIdBackImageUrl": "https://cdn/id-back.jpg",
  "licenseImageUrl": "https://cdn/license.jpg",
  "vehicleImageUrl": "https://cdn/vehicle-license.jpg",
  "personalPhotoUrl": "https://cdn/personal.jpg"
}
```

Important:

- لو البيانات كاملة يبدأ الحساب غالبًا في `UnderReview`
- لو فيه نقص يبدأ في `NeedsDocuments`
- التواريخ يجب أن تكون صالحة وغير منتهية

### 2. Get Driver Profile

- `GET /api/drivers/me/profile`

الموبايل يعتمد على هذا endpoint كـ source of truth لشاشة الملف وحالة المستندات.

Example response:

```json
{
  "fullName": "Ahmed Driver",
  "email": "driver@example.com",
  "phone": "01000000000",
  "address": "Al Khobar",
  "vehicleType": "Motorcycle",
  "licenseNumber": "DL-12345",
  "nationalIdExpiryDate": "2028-05-01T00:00:00Z",
  "driverLicenseExpiryDate": "2027-10-01T00:00:00Z",
  "vehicleLicenseNumber": "VL-99881",
  "vehicleLicenseExpiryDate": "2027-12-31T00:00:00Z",
  "nationalId": "29901011234567",
  "personalPhotoUrl": "https://cdn/personal.jpg",
  "nationalIdFrontImageUrl": "https://cdn/id-front.jpg",
  "nationalIdBackImageUrl": "https://cdn/id-back.jpg",
  "licenseImageUrl": "https://cdn/license.jpg",
  "vehicleImageUrl": "https://cdn/vehicle-license.jpg",
  "documents": [
    {
      "documentType": "NationalId",
      "status": "review",
      "rejectionReason": null,
      "reviewedAtUtc": null,
      "reviewedByName": null
    },
    {
      "documentType": "DriverLicense",
      "status": "valid",
      "rejectionReason": null,
      "reviewedAtUtc": "2026-05-07T12:00:00Z",
      "reviewedByName": "Compliance Reviewer"
    },
    {
      "documentType": "VehicleLicense",
      "status": "rejected",
      "rejectionReason": "Expiry date image is unclear",
      "reviewedAtUtc": "2026-05-07T12:05:00Z",
      "reviewedByName": "Compliance Reviewer"
    }
  ],
  "region": "EASTERN",
  "city": null,
  "regionNameAr": "المنطقة الشرقية",
  "regionNameEn": "Eastern Region",
  "cityNameAr": null,
  "cityNameEn": null,
  "verificationStatus": "NeedsDocuments",
  "accountStatus": "Pending",
  "reviewNote": "Expiry date image is unclear",
  "suspensionReason": null,
  "isProfileComplete": false,
  "completionPercent": 50,
  "missingRequirements": [
    "rejected_documents"
  ],
  "canSubmitForReview": false
}
```

### 3. Update Personal Section

- `PUT /api/drivers/me/profile/personal`

Example body:

```json
{
  "fullName": "Ahmed Driver",
  "email": "driver@example.com",
  "phone": "01000000000",
  "address": "Nasr City"
}
```

### 4. Update Vehicle And Text-Based Compliance Fields

- `PUT /api/drivers/me/profile/vehicle`

Example body:

```json
{
  "vehicleType": "Motorcycle",
  "nationalId": "29901011234567",
  "licenseNumber": "DL-12345",
  "nationalIdExpiryDate": "2028-05-01T00:00:00Z",
  "driverLicenseExpiryDate": "2027-10-01T00:00:00Z",
  "vehicleLicenseNumber": "VL-99881",
  "vehicleLicenseExpiryDate": "2027-12-31T00:00:00Z",
  "region": "EASTERN"
}
```

Important:

- هذا endpoint مسؤول عن الأرقام + تواريخ الانتهاء + المنطقة + المدينة
- لو المندوب كان `Approved` وعدل بيانات حساسة، الحساب يرجع إلى المراجعة مرة أخرى
- إذا أعاد رفع بيانات مستند كانت مرفوضة وتمت بشكل كامل، حالة مراجعة هذا المستند ترجع `Pending`

### 5. Update Documents Section

- `PUT /api/drivers/me/profile/documents`

Example body:

```json
{
  "personalPhotoUrl": "https://cdn/personal-new.jpg",
  "nationalIdFrontImageUrl": "https://cdn/id-front-new.jpg",
  "nationalIdBackImageUrl": "https://cdn/id-back-new.jpg",
  "licenseImageUrl": "https://cdn/license-new.jpg",
  "vehicleImageUrl": "https://cdn/vehicle-license-new.jpg"
}
```

Important:

- هذا endpoint مسؤول عن الصور فقط
- عند تعديل الصور، المستندات التي أصبحت complete ترجع إلى `Pending` review مرة أخرى
- لا تعتمد على local state بعد الحفظ، اعمل refresh من `GET /api/drivers/me/profile`

## Document Types Returned By Backend

داخل `documents[]` ستجد الأنواع التالية فقط:

- `NationalId`
- `DriverLicense`
- `VehicleLicense`

الموبايل يفضل يعمل mapping ثابت لها:

- `NationalId` => البطاقة الشخصية
- `DriverLicense` => رخصة القيادة
- `VehicleLicense` => رخصة المركبة

## Document Status Values

كل مستند داخل `documents[]` له `status`، والقيم الحالية هي:

- `valid`
- `review`
- `rejected`
- `expiring`

### Meaning Of Each Status

- `valid`
  - المستند مكتمل وتمت الموافقة عليه من الأدمن
- `review`
  - المستند إما تحت المراجعة أو لم يحصل بعد على موافقة نهائية
- `rejected`
  - المستند مرفوض ويجب على المندوب تعديله
- `expiring`
  - تاريخ الانتهاء منتهي بالفعل، ويجب تحديث المستند

## Mobile Profile Document Cards

في شاشة بروفايل المندوب، لا تعرض المستندات كقائمة صور فقط. كل كارت مستند يجب أن يعرض حالة واضحة للمندوب:

| `documents[].status` | Arabic label | Recommended color | Required UI |
| --- | --- | --- | --- |
| `review` | `محدث وتحت المراجعة` | Amber | Badge واضح + نص: `الملف الجديد بانتظار مراجعة الإدارة`. |
| `valid` | `مقبول` | Green | Badge قبول، ويمكن عرض `reviewedAtUtc` إن كان موجودًا. |
| `rejected` | `مرفوض` | Red | Badge رفض + إظهار `rejectionReason` داخل الكارت + زر `إعادة رفع المستند`. |
| `expiring` | `مقبول وقرب الانتهاء` | Orange | Badge تحذير + CTA لتحديث المستند. |

قواعد العرض:

- `rejectionReason` لازم يظهر بجوار الملف المرفوض مباشرة، وليس فقط داخل modal أو details screen.
- في حالة `review`، لا تستخدم كلمة `مقبول` أو أي لون أخضر؛ الملف لم يعتمد بعد.
- بعد رفع ملف جديد، اعمل reload من `GET /api/drivers/me/profile` واعرض الحالة الرسمية القادمة من `documents[]`.
- لو التطبيق يحتفظ بصورة محلية للمعاينة بعد الرفع، ضع عليها علامة `بانتظار المراجعة` ولا تستبدل الصورة المعتمدة في الكاش.
- عند الضغط على كارت مرفوض، افتح شاشة تعديل نفس `documentType` مباشرة.

## Rejection Handling In Mobile

إذا كانت `status = rejected`:

- اعرض `rejectionReason`
- افتح للمستخدم إمكانية تعديل نفس المستند مباشرة
- أظهر CTA واضح مثل:
  - `إعادة رفع المستند`
  - `تعديل البيانات`
- بعد الحفظ، اعمل reload للـ profile

## Missing Requirements Values

القيم الحالية التي قد تظهر في `missingRequirements`:

- `missing_personal_info`
- `missing_vehicle_info`
- `missing_documents`
- `expired_documents`
- `rejected_documents`
- `missing_region`

## Recommended Mobile UI Logic

### Show Profile Completion State

اعرض:

- `completionPercent`
- `verificationStatus`
- `missingRequirements`

### Show Three Separate Cards

الأفضل في UI أن يكون هناك 3 cards منفصلة:

1. البطاقة الشخصية
2. رخصة القيادة
3. رخصة المركبة

وكل card تعرض:

- رقم المستند
- تاريخ الانتهاء
- الصورة أو الصور
- حالة المراجعة
- سبب الرفض لو موجود
- زر تعديل

### National ID Special Case

البطاقة الشخصية تحتاج صورتين:

- front
- back

لذلك الـ UI لا يعاملها كصورة واحدة.

## Notifications The Driver Will Receive

المندوب سيستقبل إشعارات inbox/push عند الحالات التالية:

### 1. Final Account Approved

- نوع الإشعار الداخلي:
  - `driver_account_updated`
- معنى الرسالة:
  - تم اعتماد حساب المندوب

### 2. Final Account Rejected

- نوع الإشعار الداخلي:
  - `driver_account_updated`
- معنى الرسالة:
  - تم رفض الطلب

### 3. Additional Documents Requested

- نوع الإشعار الداخلي:
  - `driver_account_updated`
- معنى الرسالة:
  - مطلوب استكمال أو مراجعة المستندات

### 4. Single Document Approved

- نوع الإشعار الداخلي:
  - `driver_account_updated`
- `data.event`:
  - `account.document_approved`

Example data shape:

```json
{
  "screen": "account_status",
  "event": "account.document_approved",
  "driverId": "11111111-1111-1111-1111-111111111111",
  "documentType": "DriverLicense",
  "documentId": "DriverLicense",
  "verificationStatus": "UnderReview",
  "accountStatus": "Pending"
}
```

### 5. Single Document Rejected

- نوع الإشعار الداخلي:
  - `driver_account_updated`
- `data.event`:
  - `account.document_rejected`

Example data shape:

```json
{
  "screen": "account_status",
  "event": "account.document_rejected",
  "driverId": "11111111-1111-1111-1111-111111111111",
  "documentType": "VehicleLicense",
  "documentId": "VehicleLicense",
  "verificationStatus": "NeedsDocuments",
  "accountStatus": "Pending",
  "reason": "Expiry date image is unclear"
}
```

## Recommended Notification UX

لما التطبيق يقرأ notification من النوع ده:

- لو `event = account.document_approved`
  - افتح شاشة `account status`
  - حدث الـ profile
- لو `event = account.document_rejected`
  - افتح شاشة `account status`
  - highlight المستند المرفوض
  - اعرض سبب الرفض
- لو `event = account.request_docs`
  - افتح نفس الشاشة مع تنبيه عام

## Review Lifecycle Mobile Must Respect

### Case 1: Driver Uploads Everything First Time

1. المندوب يرفع كل البيانات
2. الحساب يدخل `UnderReview`
3. كل مستند غالبًا يظهر `review`
4. الأدمن يراجع كل مستند على حدة
5. بعد اعتماد كل المستندات، يظل الحساب منتظر final approval
6. عند final approval يصبح:
   - `verificationStatus = Approved`
   - `accountStatus = Active`

### Case 2: One Document Rejected

1. الأدمن يرفض مستند واحد
2. المندوب يستقبل إشعار
3. الـ profile يظهر:
   - `documents[n].status = rejected`
   - `documents[n].rejectionReason = ...`
   - `missingRequirements` تحتوي `rejected_documents`
4. المندوب يعدل المستند
5. التطبيق يعيد إرسال البيانات/الصور
6. المستند يرجع `review`

### Case 3: Expired Document

إذا كان تاريخ الانتهاء أقل من تاريخ اليوم:

- حالة المستند تكون `expiring`
- `missingRequirements` قد تحتوي `expired_documents`
- لا تعتبر حالة profile مكتملة
- لازم المندوب يغير التاريخ والمرفقات لو لزم

## Recommended Mobile Validation

قبل إرسال البيانات من الموبايل:

- لا تسمح بإرسال `nationalId` بدون front/back image
- لا تسمح بإرسال `driverLicenseExpiryDate` بدون `licenseImageUrl`
- لا تسمح بإرسال `vehicleLicenseExpiryDate` بدون `vehicleImageUrl`
- لا تسمح بتاريخ انتهاء في الماضي
- لا تسمح بحفظ onboarding بدون:
  - `vehicleLicenseNumber`
  - `region`
  - `city`
  - `personalPhotoUrl`

## Refresh Strategy

بعد أي action من التالي، يجب عمل refresh من `GET /api/drivers/me/profile`:

- بعد التسجيل
- بعد رفع أو تعديل أي مستند
- بعد استلام notification تخص الحساب أو المستندات
- عند فتح شاشة account status
- عند الرجوع من background إلى foreground

## Suggested Screen Names

لو حابين تنظيم واضح داخل الموبايل:

- `DriverOnboardingDocumentsScreen`
- `DriverAccountStatusScreen`
- `DriverDocumentEditorSheet`

## Backend Source Of Truth

لو احتجتوا ترجعوا للكود:

- `src/Zadana.Api/Modules/Delivery/Controllers/DriversController.cs`
- `src/Zadana.Api/Modules/Delivery/Controllers/DriverProfileController.cs`
- `src/Zadana.Application/Modules/Delivery/DTOs/DriverProfileReadinessFactory.cs`
- `src/Zadana.Infrastructure/Modules/Delivery/Services/DriverReadService.cs`
- `src/Zadana.Application/Modules/Delivery/Commands/ApproveDriverDocumentReview/ApproveDriverDocumentReviewCommand.cs`
- `src/Zadana.Application/Modules/Delivery/Commands/RejectDriverDocumentReview/RejectDriverDocumentReviewCommand.cs`

## Quick Delivery Checklist For Mobile Developer

- إضافة 3 document sections منفصلة
- دعم front/back للبطاقة الشخصية
- دعم حقول تواريخ الانتهاء الثلاثة
- دعم `vehicleLicenseNumber`
- قراءة `documents[]` من profile
- عرض `status` و `rejectionReason`
- عمل refresh بعد الاشعارات
- توجيه المستخدم لشاشة account status عند إشعارات المراجعة

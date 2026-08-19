# Driver Profile Contract

## Status

- `implemented`
- Updated: 2026-08-19 (Eastern metro — region-only geography)

## Purpose

هذا الملف يشرح `Unified Driver Profile API`، وهي الـ source of truth الجديدة لشاشات:

- profile
- profile completion
- documents readiness
- review readiness

## Endpoints

### 1. Get Unified Profile

- `GET /api/drivers/me/profile`

Example response:

```json
{
  "fullName": "Ahmed Driver",
  "email": "yahya123@gmail.com",
  "phone": "01289078938",
  "address": "Al Khobar",
  "vehicleType": "Motorcycle",
  "licenseNumber": "DRV-1001",
  "nationalId": "29801011234567",
  "personalPhotoUrl": "https://...",
  "nationalIdImageUrl": "https://...",
  "licenseImageUrl": "https://...",
  "vehicleImageUrl": "https://...",
  "region": "EASTERN",
  "city": null,
  "regionNameAr": "المنطقة الشرقية",
  "regionNameEn": "Eastern Region",
  "cityNameAr": null,
  "cityNameEn": null,
  "verificationStatus": "UnderReview",
  "accountStatus": "Pending",
  "reviewNote": null,
  "suspensionReason": null,
  "isProfileComplete": true,
  "completionPercent": 100,
  "missingRequirements": [],
  "canSubmitForReview": true
}
```

Notes:

- `region` is the operational geography field (`EASTERN` only at signup).
- `city` may be null; dispatch does not use it. Do not show a city picker.
- `primaryZoneId` / `zoneName` are **not** used in v2 profile APIs.

### 2. Update Personal Section

- `PUT /api/drivers/me/profile/personal`

body:

```json
{
  "fullName": "Ahmed Driver",
  "email": "yahya123@gmail.com",
  "phone": "01289078938",
  "address": "Al Khobar"
}
```

Notes:

- هذا endpoint يحدّث identity + عنوان السائق
- التعديل هنا لا يعيد السائق إلى `UnderReview` إذا كان التعديل غير حساس

### 3. Update Vehicle Section

- `PUT /api/drivers/me/profile/vehicle`

body:

```json
{
  "vehicleType": "Motorcycle",
  "nationalId": "29801011234567",
  "licenseNumber": "DRV-1001",
  "region": "EASTERN"
}
```

Notes:

- **`region` is required** — must be `EASTERN`.
- **`city` is optional** — if sent, backend stores it but ignores it for offer dispatch.
- Do **not** send `primaryZoneId`; use `region` instead.
- هذا endpoint يعتبر sensitive update
- إذا كان السائق Approved ثم عدّل vehicle/region، يرجع إلى `UnderReview`

### 4. Update Documents Section

- `PUT /api/drivers/me/profile/documents`

body:

```json
{
  "personalPhotoUrl": "https://...",
  "nationalIdImageUrl": "https://...",
  "licenseImageUrl": "https://...",
  "vehicleImageUrl": "https://..."
}
```

Notes:

- هذا endpoint أيضًا sensitive update
- أي تعديل documents بعد approval يعيد السائق إلى `UnderReview`

## Completion Logic

الـ backend يحسب readiness من السيرفر، وليس من local state.

القيم المحتملة في `missingRequirements`:

- `missing_personal_info`
- `missing_vehicle_info`
- `missing_documents`
- `missing_region`

تقريب completion الحالي:

- `100` عند عدم وجود أي نواقص
- `75` عند نقص واحد
- `50` عند نقصين
- `25` عند ثلاثة نواقص
- `0` عند أربعة أو أكثر

Geography completion: only `region = EASTERN` is required. An empty/null `city` with a valid region does **not** block completion.

## Important Notes

- شاشة profile يجب أن تعتمد على `GET /api/drivers/me/profile`
- لا تعتمد على `GET /api/drivers/auth/me` لهذه الشاشة
- `auth/me` يبقى مفيدًا للجلسة الحالية فقط، وليس profile completion source
- Operational geography UI: see `OPERATIONAL_GEOGRAPHY_HANDOFF_AR.md` — one Eastern metro field, no city dropdown
- في v1 لا توجد حقول:
  - `vehicleBrand`
  - `vehicleModel`
  - `plate image`
- رفع الصور يبقى عبر:
  - `POST /api/files/upload`
  - ثم تمرير الـ URLs إلى `/documents`

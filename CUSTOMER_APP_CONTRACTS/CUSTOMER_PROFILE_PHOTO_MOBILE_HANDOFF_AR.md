# صورة شخصية للعميل — Handoff للموبايل

## الحالة

- الباك إند: `implemented`
- المطلوب من تطبيق العميل: في شاشة البروفايل/الإعدادات، رفع أو تغيير أو حذف الصورة الشخصية وعرضها من `profilePhotoUrl`.

## المبدأ

رفع الصورة على خطوتين: أولًا رفع الملف إلى التخزين، ثم حفظ الرابط على حساب العميل. القراءة دائمًا من `GET /me`.

```text
اختيار صورة → POST /api/files/upload → PUT /me/profile-photo → حدّث الواجهة من الرد
```

## 1) رفع الملف

`POST /api/files/upload`

Authorization: Bearer (عميل مسجّل دخول) — المجلد يتطلب JWT.

### Form-data

| الحقل | القيمة |
|---|---|
| `file` | ملف الصورة |
| `directory` | `uploads/users/profile` |

### نجاح `200`

```json
{
  "url": "https://.../uploads/users/profile/....jpg",
  "uploadedAt": "2026-07-22T20:00:00Z"
}
```

### قيود

- الحجم العملي للصورة: حتى **5 MB** (تحقق المحتوى). حد الطلب العام 10 MB.
- الصيغ: `.jpg` / `.jpeg` / `.png` / `.webp` / `.gif` / `.bmp`
- بدون توكن → رفض (المجلد Authenticated فقط).
- المسار العملي: بعد تسجيل الدخول (وليس أثناء التسجيل بدون JWT).

### أخطاء شائعة للرفع

| errorCode | المعنى | سلوك الواجهة |
|---|---|---|
| `INVALID_UPLOAD_DIRECTORY` | مجلد غير مسموح | استخدم `uploads/users/profile` فقط |
| `FILE_TOO_LARGE` | الملف أكبر من الحد | اطلب صورة أصغر |
| `INVALID_FILE_EXTENSION` / `INVALID_FILE_SIGNATURE` | صيغة غير مدعومة أو محتوى غير صالح | أعد الاختيار بصيغة صورة |

## 2) حفظ رابط الصورة على الحساب

`PUT /api/customers/auth/me/profile-photo`

Authorization: Bearer (CustomerOnly)

### Body

```json
{
  "profilePhotoUrl": "https://.../uploads/users/profile/....jpg"
}
```

- لازم يكون رابط مطلق `http` أو `https`.
- الطول الأقصى: 1000 حرف.

### نجاح `200`

يعيد `CurrentUserDto` كاملًا ويتضمن:

```json
{
  "id": "...",
  "fullName": "...",
  "email": "...",
  "phone": "...",
  "role": "Customer",
  "profilePhotoUrl": "https://.../uploads/users/profile/....jpg"
}
```

حدّث الكاش المحلي للصورة من `profilePhotoUrl` في الرد مباشرة.

## 3) قراءة الصورة

`GET /api/customers/auth/me`

Authorization: Bearer (CustomerOnly)

استخدم `profilePhotoUrl` (قد يكون `null` إن لم تُرفع صورة).

## 4) حذف الصورة

`DELETE /api/customers/auth/me/profile-photo`

Authorization: Bearer (CustomerOnly)

نجاح `200` بنفس شكل `CurrentUserDto` و`profilePhotoUrl: null`.

## واجهة البروفايل المقترحة

1. دائرة/صورة في أعلى البروفايل؛ إن لم توجد صورة اعرض placeholder.
2. زر **تغيير الصورة** / **رفع صورة**:
   - اختيار من المعرض أو الكاميرا.
   - أظهر مؤشر تحميل أثناء الرفع.
   - بعد نجاح `upload` استدعِ فورًا `PUT profile-photo`.
3. زر **حذف الصورة** ظاهر فقط إذا وُجدت صورة.
4. بعد أي نجاح: حدّث المعاينة من رد الـ API (لا تعتمد على المسار المحلي فقط).

## اختبارات قبول

1. بدون تسجيل دخول → رفع `uploads/users/profile` يفشل.
2. رفع صيغة غير مدعومة → رفض بدون تغيير البروفايل.
3. رفع صورة صالحة ثم `PUT profile-photo` → `GET /me` يعيد نفس `profilePhotoUrl`.
4. `DELETE profile-photo` → `profilePhotoUrl` يصبح `null` والصورة تختفي من الواجهة.
5. رابط غير صالح في `PUT` → رفض validation دون مسح الصورة الحالية.

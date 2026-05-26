# دليل API تطبيق المندوب (Driver Mobile App)

> آخر تحديث: 2026-05-24  
> الجمهور: مبرمج تطبيق المندوب (Flutter/Native)

---

## البيئات

| البيئة | Base URL | SignalR |
|--------|----------|--------|
| Development | `http://localhost:5298/api` | `http://localhost:5298` |
| Production | `https://zadana.runasp.net/api` | `https://zadana.runasp.net` |

## المصادقة

- JWT Bearer Token: `Authorization: Bearer <access_token>`
- Access Token صلاحيته 60 دقيقة
- Policy: `DriverOnly` على كل endpoints المندوب (ما عدا register و login)

---

## 1. المصادقة (`/api/drivers/auth`)

| Method | Path | Auth | الوصف |
|--------|------|------|--------|
| POST | `/login` | ❌ | تسجيل دخول |
| POST | `/refresh-token` | ❌ | تجديد التوكن |
| POST | `/verify-otp` | ❌ | تأكيد OTP |
| POST | `/resend-otp` | ❌ | إعادة إرسال OTP |
| POST | `/forgot-password` | ❌ + CAPTCHA | نسيت كلمة المرور |
| POST | `/reset-password` | ❌ | إعادة تعيين كلمة المرور |
| POST | `/logout` | ✅ | تسجيل خروج |
| GET | `/me` | ✅ | بيانات المندوب الحالي |
| PUT | `/me` | ✅ | تحديث الملف الشخصي |
| PUT | `/me/profile-photo` | ✅ | تحديث الصورة |
| DELETE | `/me/profile-photo` | ✅ | حذف الصورة |

### تسجيل دخول
```http
POST /api/drivers/auth/login
Content-Type: application/json

{ "identifier": "+966501234567", "password": "MyPass123" }
```

---

## 2. التسجيل (`/api/drivers/register`)

```http
POST /api/drivers/register
Content-Type: application/json

{
  "fullName": "أحمد محمد",
  "email": "ahmed@example.com",
  "phone": "+966501234567",
  "password": "SecurePass1",
  "vehicleType": "Car",
  "nationalId": "1234567890",
  "licenseNumber": "DL123456",
  "nationalIdExpiryDate": "2028-01-01",
  "driverLicenseExpiryDate": "2027-06-01",
  "vehicleLicenseNumber": "VL789",
  "vehicleLicenseExpiryDate": "2027-12-01",
  "address": "الدمام، حي الشاطئ",
  "region": "EASTERN",
  "city": "DAMMAM",
  "nationalIdFrontImageUrl": "https://...",
  "nationalIdBackImageUrl": "https://...",
  "licenseImageUrl": "https://...",
  "vehicleImageUrl": "https://...",
  "personalPhotoUrl": "https://..."
}
```

> ⚠️ رفع الوثائق يتطلب Registration Upload Token أولاً. راجع `MOBILE_DRIVER_SECURITY_HANDOFF_AR.md`

---

## 3. الشاشة الرئيسية والحالة

| Method | Path | الوصف |
|--------|------|--------|
| GET | `/api/drivers/home` | الشاشة الرئيسية (عروض + مهمة حالية + إحصائيات) |
| GET | `/api/drivers/me/status` | حالة المندوب التشغيلية |
| PUT | `/api/drivers/me/availability` | تبديل متاح/غير متاح |

### تبديل التوفر
```http
PUT /api/drivers/me/availability
Authorization: Bearer <token>
Content-Type: application/json

{ "isAvailable": true }
```

---

## 4. الموقع (`/api/drivers/location`)

```http
POST /api/drivers/location
Authorization: Bearer <token>
Content-Type: application/json

{
  "latitude": 26.4207,
  "longitude": 50.0888,
  "accuracyMeters": 12.5
}
```

**معدل البث:**
- بدون طلب نشط: كل **30 ثانية**
- أثناء تنفيذ طلب: كل **5 ثوان**
- في الخلفية أثناء طلب: كل **10-15 ثانية**

---

## 5. عروض التوصيل (Offers)

| Method | Path | الوصف |
|--------|------|--------|
| POST | `/api/drivers/offers/{assignmentId}/accept` | قبول العرض |
| POST | `/api/drivers/offers/{assignmentId}/reject` | رفض العرض |

### قبول عرض
```http
POST /api/drivers/offers/guid-here/accept
Authorization: Bearer <token>
```

### رفض عرض
```http
POST /api/drivers/offers/guid-here/reject
Authorization: Bearer <token>
Content-Type: application/json

{ "reason": "بعيد جداً" }
```

> العرض يأتي عبر SignalR event `ReceiveDeliveryOffer` ومدته **60 ثانية** فقط

---

## 6. المهام (Assignments)

| Method | Path | الوصف |
|--------|------|--------|
| GET | `/api/drivers/assignments/current` | المهمة الحالية |
| GET | `/api/drivers/assignments/{id}` | تفاصيل مهمة |
| GET | `/api/drivers/assignments/history` | سجل المهام |
| POST | `/api/drivers/assignments/{id}/proof` | إثبات التسليم |
| POST | `/api/drivers/assignments/{id}/verify-otp` | تحقق OTP |
| POST | `/api/drivers/assignments/{id}/resend-otp` | إعادة إرسال OTP |

### تحقق OTP الاستلام
```http
POST /api/drivers/assignments/guid/verify-otp
Authorization: Bearer <token>
Content-Type: application/json

{ "otpType": "pickup", "otpCode": "1234" }
```

---

## 7. تحديث حالة الطلب

| Method | Path | الوصف |
|--------|------|--------|
| POST | `/api/drivers/orders/{orderId}/arrived-at-vendor` | وصلت للبائع |
| POST | `/api/drivers/orders/{orderId}/picked-up` | استلمت الطلب |
| POST | `/api/drivers/orders/{orderId}/on-the-way` | في الطريق |
| POST | `/api/drivers/orders/{orderId}/arrived-at-customer` | وصلت للعميل |
| POST | `/api/drivers/orders/{orderId}/delivered` | تم التسليم |
| POST | `/api/drivers/orders/{orderId}/delivery-failed` | فشل التسليم |

### تسلسل الحالات
```
Accepted → ArrivedAtVendor → PickedUp → OnTheWay → ArrivedAtCustomer → Delivered
```

### فشل التسليم
```http
POST /api/drivers/orders/guid/delivery-failed
Authorization: Bearer <token>
Content-Type: application/json

{ "note": "العميل غير متواجد" }
```

---

## 8. الطلبات المكتملة

| Method | Path | الوصف |
|--------|------|--------|
| GET | `/api/drivers/orders/completed` | قائمة الطلبات المكتملة |
| GET | `/api/drivers/orders/completed/{orderId}` | تفاصيل طلب مكتمل |

---

## 9. الملف الشخصي (`/api/drivers/me/profile`)

| Method | Path | الوصف |
|--------|------|--------|
| GET | `/personal` | بيانات الملف الشخصي |
| PUT | `/personal` | تحديث البيانات الشخصية |
| PUT | `/vehicle` | تحديث بيانات المركبة |
| PUT | `/documents` | تحديث الوثائق |

### تحديث المركبة
```json
{
  "vehicleType": "Car",
  "nationalId": "1234567890",
  "licenseNumber": "DL123",
  "nationalIdExpiryDate": "2028-01-01",
  "driverLicenseExpiryDate": "2027-06-01",
  "vehicleLicenseNumber": "VL789",
  "vehicleLicenseExpiryDate": "2027-12-01",
  "region": "EASTERN",
  "city": "DAMMAM"
}
```

---

## 10. المحفظة (`/api/drivers/wallet`)

| Method | Path | الوصف |
|--------|------|--------|
| GET | `/` | ملخص المحفظة |
| GET | `/transactions?page=1&pageSize=20` | الحركات المالية |
| GET | `/payment-methods` | طرق السحب |
| POST | `/payment-methods` | إضافة طريقة سحب |
| PUT | `/payment-methods/{id}` | تعديل طريقة سحب |
| DELETE | `/payment-methods/{id}` | حذف طريقة سحب |
| POST | `/payment-methods/{id}/make-primary` | تعيين كأساسية |
| POST | `/withdrawals` | طلب سحب |
| GET | `/withdrawals` | سجل طلبات السحب |

### إضافة حساب بنكي
```json
{
  "type": "BankAccount",
  "accountHolderName": "أحمد محمد",
  "accountIdentifier": "SA0380000000608010167519",
  "providerName": "الراجحي",
  "isPrimary": true
}
```

### طلب سحب
```json
{ "amount": 500.00, "paymentMethodId": "guid-optional" }
```

---

## 11. الإشعارات (`/api/drivers/notifications`)

| Method | Path | الوصف |
|--------|------|--------|
| GET | `/` | قائمة الإشعارات |
| GET | `/unread-count` | عدد غير المقروءة |
| POST | `/{id}/read` | تعليم كمقروء |
| POST | `/read-all` | تعليم الكل |
| DELETE | `/{id}` | حذف إشعار |
| DELETE | `/` | حذف الكل |
| GET | `/preferences` | إعدادات Push |
| PUT | `/preferences` | تحديث إعدادات Push |

### تسجيل جهاز Push
```http
POST /api/drivers/notifications/devices/register
Authorization: Bearer <token>
Content-Type: application/json

{
  "deviceToken": "fcm-token",
  "platform": "fcm",
  "deviceId": "unique-id",
  "deviceName": "iPhone 15",
  "appVersion": "1.0.0",
  "locale": "ar"
}
```

---

## 12. الدعم (`/api/drivers/support`)

| Method | Path | الوصف |
|--------|------|--------|
| POST | `/orders/{orderId}/report-issue` | بلاغ تشغيلي |
| POST | `/orders/{orderId}/dispute` | نزاع مالي |
| GET | `/cases?page=1&pageSize=20` | قضاياي |
| GET | `/cases/{caseId}` | تفاصيل قضية |
| POST | `/orders/{orderId}/cases/{caseId}/messages` | إرسال رد |
| POST | `/account-appeals` | طلب دعم حساب (بعد login) |
| GET | `/account-cases` | قضايا الحساب |
| GET | `/account-cases/{caseId}` | تفاصيل قضية حساب |
| POST | `/account-cases/{caseId}/messages` | رد على قضية حساب |
| GET | `/reasons/{type}` | أسباب الدعم المتاحة |

### بلاغ تشغيلي
```json
{
  "reasonCode": "customer_unavailable",
  "message": "العميل لا يرد على الهاتف",
  "attachments": [{ "fileName": "photo.jpg", "fileUrl": "https://..." }]
}
```

### دعم حساب (بدون login)
```http
POST /api/drivers/account-support/appeals
Content-Type: application/json

{
  "identifier": "+966501234567",
  "reasonCode": "account_locked",
  "message": "حسابي مقفل بدون سبب"
}
```

---

## 13. SignalR — Real-Time

### Hub: `/hubs/notifications`
| Event | الوصف |
|-------|--------|
| `ReceiveDeliveryOffer` | عرض توصيل جديد (60 ثانية للرد) |
| `ReceiveAssignmentUpdated` | تحديث على المهمة |
| `ReceiveDriverHomeUpdated` | تحديث الشاشة الرئيسية |
| `ReceiveDriverWalletUpdated` | تحديث المحفظة |
| `ReceiveDriverSupportCaseChanged` | تحديث قضية دعم |
| `ReceiveNotification` | إشعار عام |

### Hub: `/hubs/order-tracking` (اختياري — لتحسين التجربة)
| Event | الوصف |
|-------|--------|
| `ReceiveOrderTrackingStatusChanged` | تغيير حالة من التاجر/الأدمن |

**الاشتراك:**
```dart
await connection.invoke('SubscribeToOrder', args: [orderId]);
```

---

## 14. دورة حياة الطلب الكاملة

```
1. المندوب متاح (isAvailable = true)
2. يستقبل ReceiveDeliveryOffer عبر SignalR
3. يقبل: POST /offers/{id}/accept
4. يتوجه للبائع + يبث موقعه كل 5 ثوان
5. يصل للبائع: POST /orders/{id}/arrived-at-vendor
6. يتحقق من OTP الاستلام: POST /assignments/{id}/verify-otp (pickup)
7. يستلم الطلب: POST /orders/{id}/picked-up
8. في الطريق: POST /orders/{id}/on-the-way
9. يصل للعميل: POST /orders/{id}/arrived-at-customer
10. يتحقق من OTP التسليم: POST /assignments/{id}/verify-otp (delivery)
11. يسلم: POST /orders/{id}/delivered
12. يعود لوضع الانتظار (isAvailable = true تلقائياً)
```

---

## 15. أكواد الخطأ الشائعة

| Code | Status | الوصف |
|------|--------|--------|
| `DRIVER_NOT_AUTHENTICATED` | 401 | لا يوجد JWT |
| `DRIVER_NOT_READY_FOR_DISPATCH` | 400 | الحساب غير مفعل |
| `DELIVERY_OFFER_NOT_AVAILABLE` | 400 | العرض لم يعد متاح |
| `DELIVERY_OFFER_EXPIRED` | 400 | انتهت مهلة العرض |
| `NOT_ASSIGNED_TO_ORDER` | 400 | الطلب ليس مخصص لك |
| `INSUFFICIENT_WITHDRAWABLE_BALANCE` | 400 | رصيد غير كافي |
| `DRIVER_COD_DEBT_NOT_SETTLED` | 400 | مبالغ COD مستحقة |
| `DRIVER_BANK_IBAN_INVALID` | 400 | IBAN غير صالح |
| `TOKEN_REVOKED` | 401 | التوكن ملغي |
| `OTP_ACCOUNT_LOCKED` | 401 | الحساب مقفل |
| `RATE_LIMIT_EXCEEDED` | 429 | طلبات كثيرة |

---

## 16. أنواع المركبات المدعومة

```
Car, Motorcycle, Bicycle, Van, Truck
```

---

## مراجع إضافية

- `MOBILE_DRIVER_SECURITY_HANDOFF_AR.md` — تفاصيل تغييرات الأمان
- `MOBILE_DRIVER_REALTIME_TRACKING_HANDOFF_AR.md` — تفاصيل التتبع اللحظي
- `MOBILE_REALTIME_ORDER_TRACKING_HANDOFF_AR.md` — عقد SignalR الكامل

# Workflow الإشعارات في Zadana والتعديلات الأخيرة

آخر تحديث: 2026-04-22

## 1. الهدف من الملف

هذا الملف يشرح:

- كيف يعمل نظام الإشعارات في Zadana من أول نقطة إطلاق الإشعار حتى وصوله للمستخدم.
- الفرق بين `Inbox`, `Realtime SignalR`, و`OneSignal Push`.
- الـ workflow الحالي لأهم السيناريوهات.
- التعديلات الأخيرة التي تم تنفيذها على الـ backend والـ frontend لتحسين وصول الإشعارات، خصوصًا في حالة `Killed / Terminated` على الموبايل.

## 2. المكونات الأساسية

- `src/Zadana.Api/Realtime/NotificationService.cs`
  مسؤول عن حفظ الإشعار في قاعدة البيانات وإرسال realtime عبر `SignalR`.

- `src/Zadana.Api/Realtime/NotificationHub.cs`
  الـ hub الخاص بالإشعارات على المسار:
  `/hubs/notifications`

- `src/Zadana.Infrastructure/Services/OneSignalPushService.cs`
  مسؤول عن بناء وإرسال payload إلى OneSignal.

- `src/Zadana.Infrastructure/Settings/OneSignalSettings.cs`
  إعدادات OneSignal مثل `AppId`, `RestApiKey`, وقنوات Android.

- `src/Zadana.Application/Modules/Orders/Events/OrderStatusChangedHandler.cs`
  الـ workflow الأساسي لإشعارات تغيّر حالة الطلب.

- `src/Zadana.Application/Modules/Marketing/Events/BannerActivatedHandler.cs`
  مسؤول عن إشعارات العروض/البانرز للعملاء.

- `src/Zadana.Api/Modules/Social/Controllers/VendorNotificationsController.cs`
  Inbox التاجر + endpoint إرسال إشعار تجريبي للتاجر.

- `src/Zadana.Api/Modules/Identity/Controllers/AdminCustomersController.cs`
  endpoint لإرسال إشعار تجريبي للعميل من لوحة الأدمن.

- `src/Zadana.Api/Modules/Vendors/Controllers/AdminVendorsController.cs`
  endpoint لإرسال إشعار تجريبي للتاجر من لوحة الأدمن.

## 3. الطبقات الثلاثة للإشعارات

### A. Inbox Notification

هذا هو الإشعار المخزن في قاعدة البيانات ويظهر داخل التطبيق أو اللوحة.

الخطوات:

1. أي handler أو controller يستدعي:
   `INotificationService.SendToUserAsync(...)`
2. `NotificationService` يعمل sanitize للعنوان والمحتوى والبيانات.
3. يتم حفظ سجل جديد في جدول `Notifications`.
4. يتم تكوين payload realtime بنفس البيانات.
5. يتم إرسال `ReceiveNotification` إلى جروب المستخدم في `SignalR`.

النتيجة:

- الإشعار يظهر في الـ inbox.
- وإذا كان المستخدم متصلًا realtime يصل له الحدث فورًا.

### B. Realtime SignalR

هذا مسار منفصل عن الـ push.

الخطوات:

1. العميل/التاجر يتصل بـ:
   `/hubs/notifications`
2. `NotificationHub` يضيف الاتصال إلى جروب المستخدم:
   `customer-{userId}`
3. `NotificationService` يرسل إلى هذا الجروب:
   - `ReceiveNotification`
   - أو `ReceiveOrderStatusChanged`
   - أو `ReceiveBroadcast`

النتيجة:

- الجرس والـ in-app refresh يحصل فورًا طالما التطبيق أو اللوحة مفتوحة.

### C. OneSignal Push

هذا المسار مخصص لإرسال push notification حقيقية إلى الموبايل أو الويب عبر OneSignal.

الخطوات:

1. أي handler أو controller يستدعي:
   `IOneSignalPushService.SendToExternalUserAsync(...)`
   أو
   `SendToExternalUsersAsync(...)`
2. `OneSignalPushService` يبني payload مناسب حسب الـ profile.
3. يتم استهداف المستخدم عبر:
   `include_aliases.external_id`
4. يتم إرسال الطلب إلى:
   `POST https://api.onesignal.com/notifications`

النتيجة:

- الإشعار يمكن أن يظهر خارج التطبيق حتى لو التطبيق مقفول، بشرط أن payload يكون displayable وأن الجهاز مسجل بشكل صحيح في OneSignal.

## 4. Workflow السيناريوهات المهمة

## 4.1 تغيير حالة الطلب

الملف:
`src/Zadana.Application/Modules/Orders/Events/OrderStatusChangedHandler.cs`

التسلسل:

1. يحصل `OrderStatusChangedNotification`.
2. الـ handler يبني `data` موحدة تحتوي مثلًا على:
   - `orderId`
   - `orderNumber`
   - `vendorId`
   - `oldStatus`
   - `newStatus`
   - `actorRole`
   - `action`
   - `targetUrl`
3. إذا كان `NotifyCustomer = true`:
   - يتم حفظ inbox للعميل.
   - يتم إرسال realtime order status event.
   - يتم إرسال OneSignal push باستخدام:
     `OneSignalPushProfile.MobileHeadsUp`
   - السبب الحالي:
     إشعار الـ test بنفس profile كان يظهر بنجاح في حالة `Killed / Terminated`، بينما مسار
     `MobileOrderUpdates` لم يكن يظهر بنفس الثبات على الأجهزة الحالية، لذلك تم تحويل
     workflow الأوردر الفعلي مؤقتًا إلى heads-up كحل تشغيلي مباشر.
4. إذا كان `NotifyVendor = true`:
   - يتم حفظ inbox للتاجر.
   - يتم إرسال realtime event للتاجر.
   - وإذا كان السيناريو يستحق push، يتم إرسال OneSignal للتاجر.

ملاحظة:

- إشعار العميل في هذا السيناريو يستخدم profile موبايل مخصص لتحديثات الطلب.
- إشعار التاجر في السيناريو الافتراضي يذهب عبر profile `Default` إلا إذا تم تمرير profile آخر.

## 4.2 إشعارات العروض والبنرات

الملف:
`src/Zadana.Application/Modules/Marketing/Events/BannerActivatedHandler.cs`

التسلسل:

1. عند تفعيل banner جديد يتم تجهيز عنوان ومحتوى الإشعار.
2. يتم إرسال broadcast realtime لكل العملاء عبر:
   `BroadcastToAllCustomersAsync(...)`
3. يتم استخراج `externalUserIds` من الأجهزة النشطة التي:
   - `IsActive = true`
   - `NotificationsEnabled = true`
   - والدور `Customer`
4. يتم إرسال OneSignal push باستخدام:
   `OneSignalPushProfile.MobileHeadsUp`

## 4.3 إشعار تجريبي للتاجر

الملف:
`src/Zadana.Api/Modules/Social/Controllers/VendorNotificationsController.cs`

التسلسل:

1. `POST /api/vendor/notifications/test`
2. يتم إرسال inbox للتاجر عبر `SendToUserAsync`.
3. إذا كان `sendPush = true`:
   يتم إرسال OneSignal push إلى `external_id = userId`.

## 4.4 إشعار تجريبي للعميل من الأدمن

الملف:
`src/Zadana.Api/Modules/Identity/Controllers/AdminCustomersController.cs`

التسلسل:

1. `POST /api/admin/customers/{customerId}/notifications/test`
2. يتم حفظ inbox للعميل.
3. إذا كان `sendPush = true`:
   يتم إرسال push باستخدام:
   `OneSignalPushProfile.MobileHeadsUp`

## 4.5 إشعار تجريبي للتاجر من الأدمن

الملف:
`src/Zadana.Api/Modules/Vendors/Controllers/AdminVendorsController.cs`

التسلسل:

1. `POST /api/admin/vendors/{vendorId}/notifications/test`
2. يتم حفظ inbox للتاجر.
3. إذا كان `sendPush = true`:
   يتم إرسال OneSignal push للتاجر.

## 5. شكل OneSignal Payload الحالي

الـ service يبني payload من داخل:
`src/Zadana.Infrastructure/Services/OneSignalPushService.cs`

العناصر الأساسية المشتركة:

- `app_id`
- `idempotency_key`
- `collapse_id`
- `target_channel = push`
- `include_aliases.external_id`
- `headings`
- `contents`
- `data`

مهم:

- `headings` و`contents` موجودتان بالفعل على مستوى الـ root.
- هذا كان صحيحًا قبل آخر تعديل أيضًا.
- المشكلة في killed state لم تكن بسبب وضع `contents/headings` داخل `data`، بل بسبب أن mobile payload كان يحتاج displayable fields إضافية ليشبه الرسالة المرئية القادمة من OneSignal Dashboard.

## 5.1 Default Profile

يستخدم غالبًا للويب أو الإشعارات العامة التي لا تحتاج mobile channel خاص.

خصائصه:

- يحافظ على:
  - `headings`
  - `contents`
  - `data`
  - `include_aliases.external_id`
- يمكن أن يضيف:
  - `web_url`

## 5.2 MobileHeadsUp Profile

يستخدم في الإشعارات العامة للموبايل مثل:

- إشعار تجريبي للعميل
- banner/offer push

خصائصه:

- يضبط قناة Android الخاصة بالـ heads-up
- يضيف خصائص mobile delivery
- يمنع web-only shape

## 5.3 MobileOrderUpdates Profile

يستخدم في تحديثات الطلبات على الموبايل.

خصائصه:

- منطقيًا هو profile خاص بتحديثات الطلب
- يضيف خصائص mobile delivery
- يبقي custom order data داخل `data`

ملاحظة تشغيلية:

- رغم أن هذا profile ما زال موجودًا داخل `OneSignalPushService`، فإن مسار
  `OrderStatusChangedHandler` الحالي يستخدم `MobileHeadsUp` فعليًا كحل مباشر لمشكلة
  killed-state إلى أن يتم تأكيد إعدادات channel الخاصة بتحديثات الطلب داخل التطبيق.
- وتم أيضًا توحيد سلوك `MobileOrderUpdates` نفسه مؤقتًا ليستخدم نفس heads-up channel
  المستخدمة في الإشعار التجريبي الناجح، حتى لا يبقى هناك أي مسار أوردر يذهب إلى قناة مختلفة.

## 6. التعديلات الأخيرة التي تم تنفيذها

## 6.1 تحسين payload للموبايل في حالة Killed / Terminated

الملف:
`src/Zadana.Infrastructure/Services/OneSignalPushService.cs`

التعديل:

- الإبقاء على `headings` و`contents` على مستوى الـ root.
- إضافة خصائص mobile delivery للـ profiles الخاصة بالموبايل:
  - `priority = 10`
  - `android_accent_color = FF127C8C`
  - `content_available = true`
  - `mutable_content = true`
- إضافة `click_action = FLUTTER_NOTIFICATION_CLICK` داخل `data` تلقائيًا إذا لم يكن موجودًا.
- إذا كان `click_action` موجودًا أصلًا داخل `data`، لا يتم استبداله.
- تم أيضًا تحويل push الخاص بـ `OrderStatusChangedHandler` إلى
  `OneSignalPushProfile.MobileHeadsUp` لأن هذا هو المسار الذي ثبت عمليًا أنه يظهر عندما
  يكون التطبيق مغلقًا تمامًا على الأجهزة التي تم اختبارها.
- وتمت محاذاة `MobileOrderUpdates` داخل `OneSignalPushService` لنفس heads-up channel
  مؤقتًا، حتى يصبح إشعار تغيّر حالة الطلب وإشعار الـ test متطابقين في طريقة الإرسال والقناة.

الهدف:

- جعل payload أقرب إلى visual notification وليس data-only behavior.
- زيادة فرصة ظهور الإشعار عندما يكون التطبيق مغلقًا تمامًا.

## 6.2 استراتيجية Android channel في OneSignal

الملف:
`src/Zadana.Infrastructure/Services/OneSignalPushService.cs`

السبب:

- حسب توثيق OneSignal الرسمي:
  - إذا كانت القناة معرفة داخل OneSignal Dashboard نستخدم `android_channel_id`.
  - إذا كانت القناة معرفة برمجيًا داخل التطبيق نفسه نستخدم `existing_android_channel_id`.
- في Zadana أسماء القنوات المستخدمة هي:
  - `zadana_heads_up_notifications`
  - `zadana_order_updates_realtime_v2`
- وبما أن المتطلب الحالي يقول إن التطبيق نفسه يعرّف هذه القنوات، فالتنفيذ الحالي يعتمد `existing_android_channel_id` لأنه هو البارامتر الرسمي الصحيح لهذه الحالة.
- تم أيضًا استكمال قيمة `OrderUpdatesAndroidChannelId` داخل الإعدادات حتى تظل القيم موحدة وواضحة إذا تم لاحقًا نقل إدارة القنوات إلى OneSignal Dashboard.

القيم المستخدمة:

- Heads-up:
  `zadana_heads_up_notifications`
- Order updates:
  `zadana_order_updates_realtime_v2`

مهم:

- توثيق OneSignal الحالي لا يذكر بارامتر `android_visibility` داخل Create Message API.
- المقابل الرسمي لهذا السلوك موجود على مستوى Android Notification Channel نفسه تحت `Lockscreen visibility`:
  - `Public`
  - `Private`
  - `Secret`
- لذلك لو الهدف هو إظهار محتوى الإشعار على شاشة القفل، فيجب ضبط هذا على مستوى الـ channel داخل التطبيق أو داخل OneSignal category، وليس الاعتماد على payload غير موثق رسميًا.

## 6.3 تحديث الاختبارات

الملف:
`tests/Zadana.Application.Tests/Application/Social/OneSignalPushServiceTests.cs`

تمت إضافة/تحديث اختبارات للتحقق من:

- وجود `headings` و`contents` في الـ root.
- وجود channel الصحيح لكل profile.
- وجود:
  - `android_accent_color`
  - `content_available`
  - `mutable_content`
  - `click_action`
- عدم كسر الـ default payload.
- الحفاظ على `click_action` المرسل من caller إذا كان موجودًا.

## 6.4 تحسين إدارة OneSignal API Key محليًا

الملفات:

- `src/Zadana.Api/Zadana.Api.csproj`
- `src/Zadana.Api/Properties/launchSettings.json`

التعديلات:

- إضافة `UserSecretsId` للمشروع حتى يتم تحميل `OneSignal:RestApiKey` من user secrets محليًا.
- إزالة `OneSignal__RestApiKey` من `launchSettings.json` حتى لا يطغى على الـ secret الصحيح.

الهدف:

- منع تخزين المفتاح داخل ملفات متتبعة في Git.
- منع التعارض بين secret محلي وقيمة قديمة داخل launch profile.

## 6.5 تعديلات مرتبطة في الـ frontend

هذه التعديلات ليست داخل الـ backend workflow نفسه، لكنها مؤثرة في سلوك الإشعارات:

- تم توحيد `OneSignal App ID` في الـ vendor panel مع الـ backend.
- تم جعل تحميل OneSignal SDK في الـ vendor panel يفشل بشكل هادئ إذا كان المتصفح أو extension يمنع `cdn.onesignal.com`.
- تم تحسين vendor alerts inbox ليتحمل transient network failures مثل `status: 0` و`ERR_HTTP2_PING_FAILED` بدون إسقاط الحالة بالكامل.

## 7. كيف أتحقق أن الـ workflow يعمل

### تحقق من الـ inbox

- نفذ endpoint الإشعار التجريبي.
- افحص:
  - `GET /api/vendor/notifications`
  - أو `GET /api/notifications`

### تحقق من realtime

- افتح اتصال `SignalR` على:
  `/hubs/notifications`
- تأكد من وصول:
  - `ReceiveNotification`
  - أو `ReceiveOrderStatusChanged`

### تحقق من OneSignal push

- تأكد أن:
  - `OneSignal.Enabled = true`
  - `AppId` صحيح
  - `RestApiKey` صحيح
  - الجهاز مسجل في OneSignal
  - `external_id` في OneSignal يساوي `userId`
- أرسل endpoint تجريبي ثم افحص response من OneSignal.

## 8. أوامر مفيدة أثناء التطوير

تشغيل الاختبارات الخاصة بـ OneSignal فقط:

```powershell
dotnet test 'Zadana-Backend/tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj' --filter OneSignalPushServiceTests
```

إذا كان هناك process شغال ماسك ملفات build، يمكن استخدام output path منفصل للاختبار:

```powershell
dotnet test 'Zadana-Backend/tests/Zadana.Application.Tests/Zadana.Application.Tests.csproj' --filter OneSignalPushServiceTests -p:BaseOutputPath=D:\zadana-test-bin\ -p:BaseIntermediateOutputPath=D:\zadana-test-obj\
```

## 9. ملاحظات مهمة

- OneSignal Dashboard غالبًا يرسل payload مرئي كامل بشكل افتراضي، لذلك قد ينجح في killed state حتى لو الـ backend payload ناقص.
- الـ backend الآن يرسل mobile payload أوضح وأقرب لسلوك dashboard في سيناريوهات الموبايل.
- إذا ظل killed-state لا يعمل بعد هذه التعديلات، فالأولوية التالية في الفحص تكون داخل Flutter:
  - تهيئة القنوات نفسها
  - ربط `external_id`
  - تفعيل استقبال الإشعار من OneSignal SDK
  - إعدادات الخلفية/الـ permission على Android وiOS

## 10. ملخص سريع

- `NotificationService` = حفظ + realtime
- `NotificationHub` = قناة realtime
- `OneSignalPushService` = push provider integration
- `OrderStatusChangedHandler` = أهم workflow للطلبات
- `BannerActivatedHandler` = broadcast + mobile heads-up
- التعديل الأخير الأهم = تقوية mobile payload ليظهر بشكل أفضل عندما يكون التطبيق مغلقًا

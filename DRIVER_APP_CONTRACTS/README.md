# Driver App Contracts

هذه الحزمة مخصصة لمبرمج الموبايل، وتشرح الـ APIs التي تم تنفيذها فعليًا للـ driver app بشكل منفصل وواضح.

الملفات:

- `ORDER_DETAILS_CONTRACT.md`
- `COMPLETED_ORDERS_CONTRACT.md`
- `WALLET_CONTRACT.md`
- `PROFILE_CONTRACT.md`
- `NOTIFICATIONS_CONTRACT.md`

ملاحظات عامة:

- كل الـ endpoints هنا تتطلب:
  - `Authorization: Bearer <access_token>`
  - سائق authenticated تحت policy `DriverOnly`
- كل الـ paths هنا relative إلى `API_BASE_URL`
- هذه الملفات تعكس الـ backend الحالي، وليست mock contracts

الملفات المصدرية الأساسية في الـ backend:

- `src/Zadana.Api/Modules/Delivery/Controllers/DriversController.cs`
- `src/Zadana.Api/Modules/Delivery/Controllers/DriverProfileController.cs`
- `src/Zadana.Api/Modules/Delivery/Controllers/DriverWalletController.cs`
- `src/Zadana.Api/Modules/Delivery/Controllers/DriverNotificationsController.cs`
- `src/Zadana.Infrastructure/Modules/Delivery/Services/DriverReadService.cs`

لو احتجت flow أوسع للتسجيل والتشغيل الأساسي، راجع أيضًا:

- `DRIVER_MOBILE_API_CONTRACT.md`

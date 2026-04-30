# Customer App Contracts

This folder documents the customer-facing backend changes that are already implemented and should be used as the source of truth by the mobile team.

Files:

- `CHECKOUT_CONTRACT.md`
- `ORDER_TRACKING_CONTRACT.md`
- `ORDER_DETAILS_CONTRACT.md`
- `ORDER_SUPPORT_CASES_CONTRACT.md`
- `NOTIFICATIONS_CONTRACT.md`
- `PAYMENT_CONTRACT.md`
- `SIGNALR_CONTRACT.md`

General notes:

- All endpoints here require:
  - `Authorization: Bearer <access_token>`
  - authenticated customer under policy `CustomerOnly`
- All paths are relative to `API_BASE_URL`
- These files describe the current backend behavior, not mock contracts

Primary backend sources:

- `src/Zadana.Api/Modules/Orders/Controllers/CheckoutController.cs`
- `src/Zadana.Api/Modules/Orders/Controllers/OrdersController.cs`
- `src/Zadana.Api/Modules/Orders/Requests/CheckoutRequests.cs`
- `src/Zadana.Api/Modules/Orders/Requests/MyOrdersRequests.cs`
- `src/Zadana.Application/Modules/Checkout/DTOs/CheckoutDtos.cs`
- `src/Zadana.Application/Modules/Orders/DTOs/OrderDtos.cs`

# Driver App Contracts

This folder is prepared for the mobile developer and documents the driver app APIs that are already implemented in the current backend.

## Available Contracts

- `HOME_CONTRACT.md`
- `ORDER_DETAILS_CONTRACT.md`
- `COMPLETED_ORDERS_CONTRACT.md`
- `WALLET_CONTRACT.md`
- `PROFILE_CONTRACT.md`
- `NOTIFICATIONS_CONTRACT.md`

## Shared Notes

- All authenticated endpoints here require:
  - `Authorization: Bearer <access_token>`
  - an authenticated driver under the `DriverOnly` policy
- All endpoint paths are relative to `API_BASE_URL`
- These files describe the real backend implementation, not local mocks

## Main Backend Sources

- `src/Zadana.Api/Modules/Delivery/Controllers/DriversController.cs`
- `src/Zadana.Api/Modules/Delivery/Controllers/DriverProfileController.cs`
- `src/Zadana.Api/Modules/Delivery/Controllers/DriverWalletController.cs`
- `src/Zadana.Api/Modules/Delivery/Controllers/DriverNotificationsController.cs`
- `src/Zadana.Infrastructure/Modules/Delivery/Services/DriverReadService.cs`

For the wider registration and operational flow, also see:

- `DRIVER_MOBILE_API_CONTRACT.md`

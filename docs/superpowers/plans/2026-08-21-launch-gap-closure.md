# Zadna Launch Gap Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close all remaining Important re-audit gaps so Eastern launch branches are product-ready (OTP SMS/WhatsApp still out of scope).

**Architecture:** Keep GPS ≤50 km Haversine as the matching rule everywhere (checkout radius, dispatch, admin assign, home offers, live pricing origin). Bank verification stays inside vendor/driver review — remove the empty Super Panel IBAN placeholder route. Ship Backend + Super + Vendor feature branches together.

**Tech Stack:** .NET 9 backend worktree; Angular Super Panel; Angular Vendor Panel.

## Global Constraints

- Out of scope: OTP SMS, WhatsApp OTP, enabling Wapilot/WhatsApp Cloud for registration OTP, pickup SMS TODOs.
- Currency is SAR / ر.س only.
- `DeliveryProximityLimits.MaxMatchKm = 50` is the matching radius; do not reintroduce city-lock for matching.
- Bank IBAN: do NOT build a third verification queue; hide/remove placeholder; keep verify-in-review flows.
- Commit on each repo's feature branch; do not merge/push unless asked.
- Work paths:
  - Backend: `F:/Zadna/Zadana_Backend/.worktrees/eastern-proximity-dispatch` branch `feature/eastern-proximity-dispatch`
  - Super: `F:/Zadna/zadan_super_panel` branch `feature/eastern-proximity-dispatch`
  - Vendor: `F:/Zadna/zadana_vendor_panel` branch `fix/gap-closure-vendor`

---

## Task 1: Backend proximity leftovers

**Files:**
- `NearestBranchSelector.cs` / callers in `CheckoutSupport.cs`, `CartBranchSelectionSupport.cs`
- `CheckoutSupport.IsOutsideBranchRadius`
- `DeliveryPricingService.ResolveDriverOriginAsync`
- `DeliveryPickupAreaMatcher.DriverMatchesDeliveryArea` or admin/home callers
- `AdminOrdersController` assign path
- `DriverHomeReadService`
- `Program.cs` Wapilot prod secret guard
- Tests under `tests/Zadana.Application.Tests`

- [ ] **Step 1:** No-coords customer address must NOT bind primary branch anywhere — return empty / force null branch selection when lat/lng missing (city-only).
- [ ] **Step 2:** Remove same-city early return in `IsOutsideBranchRadius` so MaxMatchKm always applies when coords exist.
- [ ] **Step 3:** Live pricing driver origin: stop city-locking; allow region-only (`city: null`) drivers with fresh GPS near pickup (same ≤50 km rule).
- [ ] **Step 4:** Admin assign + driver home: replace always-true `DriverMatchesDeliveryArea` with `DriverMatchesPickup` (fresh GPS ≤50 km). Update error codes/strings from city → proximity where cheap.
- [ ] **Step 5:** When `WapilotOtp:Enabled` in Production, also require `WapilotOtp:WebhookSecret`.
- [ ] **Step 6:** (Minor if cheap) Pickup branch listing: prefer proximity/region over city-only filter.
- [ ] **Step 7:** Add/adjust unit tests; run Application.Tests; commit.

## Task 2: Super Panel IBAN placeholder + permissions

**Files:** bank-account-verification route/nav; `PERMISSION_GROUPS`; pricing permission if still `finances.manage_settings`.

- [ ] **Step 1:** Remove or hard-hide Bank Account Verification placeholder from routes/nav/screen map; keep vendor + driver review verification as source of truth.
- [ ] **Step 2:** Add `delivery_settings` and `wallets` to `PERMISSION_GROUPS` if missing; align pricing/settings gates with backend permission names.
- [ ] **Step 3:** Build check; commit.

## Task 3: Vendor Panel leftover Important gaps

**Files:** i18n currency (partially done), catalog HQ branch mapping, onboarding radius, dead order-create.

- [ ] **Step 1:** Confirm SAR currency i18n (ORDERS.CURRENCY / COMMON.EGP alias) — commit if uncommitted.
- [ ] **Step 2:** HQ catalog: ensure `VendorBranchId` resolves for HQ scope (not only VendorBranch scope) when creating/updating products.
- [ ] **Step 3:** Onboarding `branchDeliveryRadiusKm: 5` → `50` to match MaxMatchKm product rule (or remove hardcoded mismatch).
- [ ] **Step 4:** Remove dead order-create files/route leftovers if still present.
- [ ] **Step 5:** Build check; commit.

## Task 4: Verification

- [ ] Smoke: MaxMatchKm=50, DriverMatchesPickup used by admin/home/dispatch, no EGP in EN orders, no IBAN placeholder route.
- [ ] Update reaudit notes / canvas statuses mentally for closed items.

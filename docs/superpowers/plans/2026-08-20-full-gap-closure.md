# Zadna Full Gap Closure — 4 Parallel Phases

**Out of scope:** OTP SMS, WhatsApp OTP, Wapilot/WhatsApp Cloud OTP enablement, pickup SMS TODOs.

**Done when:** Critical/Important audit gaps closed (except excluded), tests/builds green, feature branches ready for PR.

## Phase 1 — Eastern proximity (backend worktree)
Already largely done on `feature/eastern-proximity-dispatch`. Verify MaxMatchKm=50, region-only drivers, nearest branch, GPS dispatch. No re-implementation unless missing.

## Phase 2 — Vendor panel (`main` → branch `fix/gap-closure-vendor`)
- Remove or disable fake manual order create route/search
- Mark/stop fake category campaigns from counting as live; coupons deactivate/delete
- EGP → CURRENCY (SAR)
- Completed filter → DELIVERED
- Orders: stop 250 client cache swallow; use API paging if feasible cheaply
- Map: Dammam default; Eastern bounds; replace public OSRM with straight line
- Staff deny redirect; employee branch UX honesty
- HQ branchId mapping if cheap

## Phase 3 — Super panel (`feature/eastern-proximity-dispatch`)
- Toast dispatch/order errors; gate on orders.edit
- missing_region i18n; reduce city-as-identity
- Pricing permission alignment if cheap
- Hide Save without system.manage_settings on pickup
- Wallet adjust wallets.edit
- Remove fake Network Health 99.9% or label demo
- Bank placeholder: hide or minimal wire

## Phase 4 — Backend hardening (same worktree as Phase 1)
- Wapilot webhook fail-closed if secret empty
- Require BotChallenge secret in Production
- Drop .com email TLD lock
- Payment inbox poison alert/logging after MaxAttempts
- COD threshold in appsettings
- SQL health on /health/ready if cheap
- Contract docs: PROFILE/HOME primaryZoneId cleanup; DOCUMENTS RIYADH examples
- Rename pickup-city-mismatch reason to proximity if leftover
- Do NOT enable SMS/WhatsApp OTP

# Admin Driver Detail Contract

## Status

- `implemented`

## Purpose

This contract documents the backend-first integration for the superadmin driver detail page:

- `GET /api/admin/drivers/{id}` is the source of truth for `/drivers/:id`
- frontend detail tabs must render backend data or explicit empty states
- frontend must not use `drivers.mock.ts` or cached list data as a fallback for detail pages

## Main Endpoint

### `GET /api/admin/drivers/{id}`

Authentication:

- `Authorization: Bearer <access_token>`
- policy: `AdminOnly`

The response is `AdminDriverDetailDto`.

The top-level DTO contains compatibility fields for the hero/list summary plus detailed sections for each tab:

- `overview`
- `workflow`
- `operations`
- `performanceDetails`
- `support`
- `compliance`
- `financeDetails`
- `verification`
- `profileReadiness`

## Profile Readiness

`profileReadiness` explains whether the driver profile can be reviewed or dispatched.

Shape:

```json
{
  "isProfileComplete": false,
  "completionPercent": 75,
  "missingRequirements": ["missing_documents"],
  "canSubmitForReview": false,
  "checklist": [
    {
      "code": "personal_info",
      "completed": true,
      "note": null,
      "critical": false
    },
    {
      "code": "personal_photo",
      "completed": false,
      "note": "missing_document_note",
      "critical": true
    }
  ]
}
```

Checklist codes:

- `personal_info`
- `vehicle_info`
- `national_id_document`
- `license_document`
- `vehicle_document`
- `personal_photo`
- `zone_selection`

Missing requirement codes:

- `missing_personal_info`
- `missing_vehicle_info`
- `missing_documents`
- `missing_zone_selection`

## Frontend Rules

- Use `profileReadiness` and `verification.checklist` for the verification tab.
- Use `operations.taskAssignments` for the operations table.
- Use `financeDetails.entries` for the finance ledger.
- Use `support.notes`, `support.followUps`, and `incidents` derived from backend data only.
- If a value has no backend source, render an empty state or `COMMON.NOT_AVAILABLE`.
- Do not invent ranks, speed, weekly charts, map previews, or fake support tickets client-side.

## Actions

After every mutation, reload `GET /api/admin/drivers/{id}`.

Actions:

- `POST /api/admin/drivers/{id}/review`
- `POST /api/admin/drivers/{id}/suspend`
- `POST /api/admin/drivers/{id}/reactivate`
- `POST /api/admin/drivers/{id}/notes`

Review request:

```json
{
  "action": "request-docs",
  "note": "Missing required documents"
}
```

Supported review actions:

- `approve`
- `request-docs`
- `reject`

## Acceptance Criteria

- Driver detail page never shows mock detail data.
- API failure shows an error state with retry.
- `NeedsDocuments` shows exact missing requirements and checklist.
- Uploaded document URLs are displayed directly.
- Review/suspend/reactivate/add-note actions reload the page from backend.

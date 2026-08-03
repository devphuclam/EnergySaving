# Contract: Operational Workspace UI State Model

User-facing copy is Vietnamese. Code symbols, route values, and server error codes remain English.
No page displays local fallback/demo data after an API failure.

## Landing states

| Server `landing` | Required UI | Prohibited UI |
|---|---|---|
| `SetupWizard` | Open wizard at first incomplete step | Dashboard counts inferred from local lists |
| `ContinueSetup` | Show completed step, next action, and Continue Setup | Restarting at step 1 without persisted reason |
| `Dashboard` | Navigate to Operational Dashboard | Auto-select or auto-start Simulator |
| `NoAuthorizedScope` | “Bạn chưa được cấp phạm vi truy cập” | Global counts, root Site create for Engineer |
| `DependencyError` | Dependency error, retry, correlation ID when safe | Fake data or stale local completion |

## Operational Dashboard and Audit query states

The Dashboard response uses `Ready`, `NoAuthorizedScope`, `DependencyError`, `Forbidden`, and
`RuntimeError`; these map to the common request states above and never imply a client-side fallback.
The Audit route uses the same common request states. `AUDIT_CORRELATION` is granted only to
Administrators, so a non-Administrator response omits correlation IDs rather than masking them in
the browser.

On the default Administrator Dashboard, render the visible Vietnamese action `Tạo chuỗi cấu hình
mới`. It is hidden for Engineers and other roles. Activating it navigates to `/setup?mode=new` and
reloads the empty `SetupWizard` projection from the server. Site creation must use the ID returned
by the create response and navigate to `/setup?selectedSiteId=<uuid>`; refresh reconstructs that
selection from the URL and server authorization. No list index, localStorage value, or client
supplied role/scope is authoritative.

## Wizard layout

- Desktop: horizontal eight-step progress indicator, main form panel, side summary/validation panel.
- Narrow/tablet: accessible compact step list before form content.
- Actions: Back, Save and Continue, Cancel, Retry where applicable.
- Administrator step 1: editable Site and Engineer assignment.
- Engineer step 1: assigned Site is read-only.
- Completion: navigate to Simulator and show explicit Source/configuration selection with Start
  untouched.

## Common request states

Each page or form models:

- `idle`
- `loading`
- `submitting`
- `success`
- `empty`
- `validation`
- `conflict`
- `forbidden`
- `notFound`
- `dependencyConflict`
- `runtimeError`

State is represented by text/icon/structure, never color alone. Focus moves to the first invalid
field for validation and to the error summary for cross-step failures.

## Conflict contract

When the server returns optimistic version conflict:

1. Keep the user’s unsaved form values in memory.
2. Stop activation or continuation.
3. Display current-version conflict in Vietnamese with safe error code.
4. Offer Reload current data; compare when sufficient safe fields exist.
5. Never retry automatically with a new version or silently overwrite.

## Partial activation contract

After each activation response, reload Operational Workspace Status. On failure:

- stop later steps;
- mark only server-confirmed states Active;
- show the exact failed logical step and safe error;
- preserve committed Draft/Active entities;
- retry the same uncertain request with the same idempotency key;
- revalidate and use current versions before a corrected deliberate retry.

## Navigation contract

- Wizard completion → `/simulator`.
- No Source/configuration is selected from index zero.
- No Start request occurs from wizard, landing router, route loader, or effect.
- Existing decorative Configuration actions become setup navigation in Phase 1 only when required
  for the vertical slice; remaining management actions are Phase 2.

## Accessibility contract

- All form controls have visible Vietnamese labels and associated helper/error text.
- Step state includes label and text, not color only.
- Focus order follows step → error summary → form → actions.
- Keyboard users can complete all Phase 1 actions.
- Deactivating/discarding safe Draft actions require confirmation.
- Minimum pointer target and responsive behavior follow DOC-08 Industrial Light guidance.

## Frontend evidence contract

- `npm run lint`: runnable static evidence.
- `npm run build`: runnable TypeScript/Vite evidence.
- Existing frontend behavior source: may be authored only if runnable with approved dependencies.
- Behavior-runner absence: `BLOCKED` / `BLOCKED_BY_PACKAGE_POLICY`, not PASS.

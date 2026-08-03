# Feature 003 Phase 6 — accessibility audit (T074)

Date: 2026-08-03
Scope: every Feature 003 Web surface — Login, Setup, Configuration management, Simulator,
Latest/Health, Operational Dashboard, and Audit.

## Audit method

The audit combines source inspection of `src/Web/src` with the existing static verification and Web
lint/build seams. No new package or browser runner was installed. Vietnamese is the user-facing
language; technical identifiers and status codes remain English as required by FR-026.

| Surface | Vietnamese content | Keyboard/tab/focus | Names, labels, errors | Loading/empty/error/conflict | Responsive/non-colour status | Result |
|---|---|---|---|---|---|---|
| Login/AppShell | PASS — sign-in, session, scope and errors are Vietnamese | PASS — native inputs/buttons; visible `:focus-visible`; login controls have visible Vietnamese `label`/`id` association; invalid credentials focus the first field | PASS — `sign-in-username` and `sign-in-password` are associated with `sign-in-error` through `aria-describedby`; session notices use status/alert roles | PASS — loading, submitting, invalid, forbidden, expired and runtime states are explicit | PASS — topbar/sidebar collapse at tablet width; status is text plus visual treatment | PASS |
| Setup Wizard | PASS — eight-step labels and actions are Vietnamese | PASS — native form controls/buttons; first invalid field is focused through refs; compact step list remains reachable | PASS — labels wrap controls; validation and retry notices use alert/status semantics | PASS — dependency, validation, not-found, runtime, loading, retry, conflict feedback and no-fallback states are explicit | PASS — responsive cards and visible focus outlines; completed/current step uses text and border, not colour alone | PASS |
| Configuration management | PASS — filters/actions/states are Vietnamese | PASS — buttons are native; editor focuses first invalid field; tabs are buttons and remain keyboard reachable | PASS — filter labels, editor labels, dialog headings, table region and feedback roles are present | PASS — loading, empty, forbidden, expired, dependency, runtime, validation and conflict states are explicit | PASS — horizontal table scroll and responsive form/card layout; status/action text is not colour-only | PASS |
| Simulator | PASS — selection and operation actions are Vietnamese | PASS — native selects/buttons and visible focus; no implicit selection | PASS — every selector has a matching label/`htmlFor`; retry/status notices use status role | PASS — loading, no-selection, dependency, runtime, validation, conflict and retry states are explicit | PASS — cards stack at tablet width; Run status has text (`Running`, `Paused`, `Stopped`) plus dot | PASS |
| Latest/Health | PASS — selector, refresh, health and No Data wording are Vietnamese | PASS — hierarchy selectors, search, pagination, checkbox and refresh controls are native; coordinator does not trap focus | PASS — selector labels and refresh group label; alert/status messages; explicit No Data text | PASS — loading, no selection, forbidden, dependency, runtime, not-found and stale-safe states are explicit | PASS — two-up cards collapse; values include quality/unit/timestamps and are not colour-only | PASS |
| Dashboard | PASS — operational summary and Continue Setup wording are Vietnamese | PASS — navigation/actions are native buttons/links; no decorative action is left without behavior | PASS — page heading, card regions and runtime state are textual | PASS — dependency/error/no-scope/incomplete setup states are server-derived and no fallback is shown | PASS — grid collapses at tablet; counts/status labels remain textual | PASS |
| Audit | PASS — filters, pagination, redaction and empty/error copy are Vietnamese | PASS — native filter fields, submit and cursor pagination; no keyboard-only trap found | PASS — labels for all filters, results region, alert/status states, table headings | PASS — validation, loading, forbidden, dependency, runtime and empty states are explicit | PASS — scrollable table and responsive content; redaction and permission state are text (`Ẩn theo quyền`) | PASS |

## Corrective change and red/green boundary

Source inspection found one actionable WCAG-adjacent gap: the Login inputs had an accessible name
through `aria-label` but no visible label/control association. The fix adds visible Vietnamese
labels and stable `id`/`htmlFor` pairs in `src/Web/src/app/AppShell.tsx`, localizes the navigation
and authentication region names, associates the invalid-credentials alert with both controls, and
focuses the username field when that error is shown. The labels remain compact and wrap at tablet
width. This is an accessibility-only correction with no new dependency or authorization behavior.
Web lint/build and the existing AppShell contract are the green verification seams.

No other runnable accessibility finding required production changes. Destructive lifecycle and
Draft-delete actions already require explicit confirmation. No page infers selection from array
order, and no status is communicated by colour alone: text/status codes accompany dots, badges,
borders, and notices.

## Scope and limitations

- A package-policy-blocked frontend behavior runner is not installed and was not downloaded.
- An approved authenticated browser runner is unavailable for this Phase 6 run; historical Chrome
  journeys remain cited in the phase checkpoints and are not relabeled as fresh Phase 6 evidence.
- This audit does not authorize work outside Feature 003 or any post-T080 capability.

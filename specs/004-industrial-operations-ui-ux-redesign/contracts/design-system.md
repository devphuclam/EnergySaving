# Design System: Evidence-First Industrial Light

**Feature**: 004 Industrial Operations UI/UX Redesign
**Created**: 2026-08-04
**Authority**: DOC-08 (sections 5, 6, 11, 12, 13, 14, 15) is authoritative for visual direction and
tokens; Feature 004 spec FR-016..FR-021 and the locked clarifications are normative. UI UX Pro Max
skill datasets (styles/typography/colors/ux-guidelines/charts/motion/icons/reaction) were analyzed
and reconciled against DOC-08 and repository policy.

## 1. Direction

Evidence-First Industrial Light (DOC-08 UX-D01, section 6):
professional, technical, calm, trustworthy; light-gray application canvas; white surfaces; navy
sidebar; disciplined primary blue; borders and spacing create hierarchy; very light shadow; subtle
6–8px radii; red/orange/amber reserved for exception, severity, and warning. Dark mode is deferred
and excluded from MVP-1 (UX-D10; locked clarification 1). The `prefers-color-scheme: dark` block in
`src/Web/src/index.css` is removed so no silent dark surface can appear.

## 2. Anti-patterns rejected

| Anti-pattern | Rejection basis |
|---|---|
| Marketing landing-page layout / bento grid / giant metric cards | DOC-08 6.2, UX-P02; prompt §4 |
| Exaggerated minimalism, oversized typography | DOC-08 6.1 (no soft cards/gradients); prompt §4 |
| Glassmorphism, neon/cyberpunk, decorative gradients | DOC-08 6.1; UX Pro Max rejected styles |
| Excessive shadow / corner radius | DOC-08 6.1 (shadow very light; radius 6–8px) |
| Emoji icons | DOC-08 6.1; prompt §4; icon contract uses inline SVG outline |
| Large gauge for every metric / decorative pie charts | DOC-08 6.2 |
| Missing shown as 0 or chart connected across gaps | DOC-08 6.2 UX-D04; FR-012 |
| Color-only status | UX-D07, FR-016 |
| Unsupported packages/fonts | repository dependency policy |
| Multi-step modal for long workflows | DOC-08 UX-D08 (page/wizard/drawer) |
| Role-specific separate applications | DOC-08 6.2 (consistency) |

## 3. Semantic color roles (DOC-08 11.1 authoritative)

| Token | Value | Used for |
|---|---|---|
| `primary-700` | `#1F4E78` | Sidebar, primary action, header |
| `primary-500` | `#2F75B5` | Chart line, link, selected state |
| `primary-100` | `#D9EAF7` | Info surface / selected background |
| `app-bg` | `#F4F7FA` | Main canvas |
| `surface` | `#FFFFFF` | Card/table/form |
| `text-primary` | `#17212B` | Primary content |
| `text-secondary` | `#596675` | Metadata/helper |
| `border` | `#D6DEE6` | Card/table/control |
| `success` | `#2E7D32` | Good/Healthy/Completed |
| `warning` | `#B26A00` | Uncertain/Stale/attention |
| `danger` | `#C62828` | Bad/High error/destructive |
| `critical` | `#7F1D1D` | Critical severity only |
| `missing` | `#667085` | Missing/unknown/gap |

Semantic role names (per `src/Web` existing CSS convention, renamed from ad-hoc
`--ink/--muted/--line/--panel/--canvas/--blue/--green/--amber/--red`): `--color-canvas`,
`--color-surface`, `--color-surface-elevated` (surface + 1px border, no heavy shadow),
`--color-border`, `--color-text-primary`, `--color-text-secondary`, `--color-text-muted`,
`--color-primary`, `--color-primary-action`, `--color-focus`, `--color-success`, `--color-warning`,
`--color-danger`, `--color-critical`, `--color-missing`, `--color-info` (= `primary-100`).

### Operational state semantics (each has text + icon/shape + name; never color alone — FR-016)

| State | Text label | Non-color cue | Color |
|---|---|---|---|
| Good (Data Quality) | "Tốt" + reason n/a | circle-check icon | success |
| Uncertain | "Không chắc chắn" + reason | warning triangle icon | warning |
| Bad | "Xấu/Mất giá trị" + reason | x-circle icon | danger |
| No Data/Missing | "Không có dữ liệu" + last-seen/expected/elapsed | gap glyph + dash | missing |
| Stale | "Cũ (stale)" + last value time | clock icon | warning |
| Blocked | "Bị chặn" + reason + next action | lock/stop icon | warning/text |
| Forbidden | "Không được phép" + next permitted action | lock icon + text | neutral text |
| Conflict | "Có xung đột" + reload | refresh icon | warning |
| Pending | "Đang chờ" | hourglass/spinner | primary |
| Processing | "Đang xử lý" | spinner | primary |
| Completed with errors | "Hoàn tất có lỗi" + count/reason | partial-check icon | warning |
| Retryable | "Có thể thử lại" + action | plug/rotate icon | warning |

## 4. Typography (DOC-08 11.2; no font download)

Keep the existing system stack (Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont,
Segoe UI, sans-serif); no webfont. DOC-08 sizing applied via CSS classes:

| Token | Size / weight | Use |
|---|---|---|
| `--fs-display` | 28–32px / 700–800 | KPI value with context (rare) |
| `--fs-page` | 24px / 700–800 | Page title |
| `--fs-section` | 18px / 700 | Section heading |
| `--fs-card` | 13–14px / 700 | Card / region title |
| `--fs-body` | 14px / 400–500 | Default content |
| `--fs-table` | 12–13px / 400–600 | Table, metadata, timestamps, helpers |
| `--fs-mono` | 12–13px / 400 | Point code, source key, correlation ID (`font-family: ui-monospace, ...`) |

- `font-variant-numeric: tabular-nums` on numeric/count/timestamp cells and chart axes so digits
  align during prolonged scanning (aligns with Data-Dense Dashboard guidance).
- Vietnamese-first labels (FR-021); technical identifiers/reason codes may remain English.
- `index.html` `lang` set to `vi`.

## 5. Spacing, density, radius, elevation, focus (DOC-08 11.3)

- Spacing scale: 4 / 8 / 12 / 16 / 24 / 32 / 40 / 48.
- Desktop content: 24px page padding; 12-column grid; 12–16px gutters.
- Card: padding 14–16px; radius 6–8px; 1px border (`--color-border`); shadow very light.
- Button/control: height 36–40px desktop; min target 36px; 44px on tablet/mobile touch.
- Form field: label above; helper/error below; 6–8px vertical gap.
- Compact table row: 40–44px (FR-027, DOC-08 "40–48px tùy density"); primary content ~14px;
  metadata ~12–13px. Applies only to operational data tables/lists, not detail panels, forms,
  dialogs, or explanatory content (FR-027). No density switch.
- Radius scale: 4 / 6 / 8 (control/card/drawer); no pill shapes.
- Elevation: level-0 none, level-1 1px border + subtle shadow (cards), level-2 drawer/dialog
  overlay shadow; no large blur shadows.
- Focus ring: 2px `--color-primary` ring + 3px `--color-primary-100` outer halo on `:focus-visible`;
  never removed without replacement. Focus/Default/Hover/Focus/Selected/Disabled/Read-only states
  for every interactive control (DOC-08 25.1).

## 6. Icons

- Inline SVG only, stroke-based outline style (16px inline/metadata, 20px action/navigation),
  `currentColor` fill; decorative icons `aria-hidden="true"`; interactive icon-only buttons carry
  an accessible name and tooltip/flyout (FR-020/SC-014). No icon package (D-006). Horizontal
  alignment with tabular text baselines.

## 7. Charts (DOC-08 15; FR-006/011/012/018)

- Small self-authored SVG chart components; no chart package (D-007).
- Every chart shows: metric, unit, timezone, data cutoff, grain, quality, coverage (UX-P01).
- Missing = visible gap; never zero, never interpolated (UX-D04, FR-012).
- Threshold as dashed line with label/version; alert window as light background band; markers
  shaped (not color-only) for Uncertain/Bad.
- No dual axis unless reviewed; no 3D; pie/donut only for small distribution, not time-series.
- Textual/table alternative for important chart content (FR-020).

## 8. Motion

- Micro-interactions 150–300ms, ease-out; hover displacement < 2px; transform/opacity only;
  respect `prefers-reduced-motion: reduce` (freeze decorative motion, retain essential state
  changes). Loading uses skeleton/spinner, not continuous animation of real data (UX guidelines).

## 9. Responsive tokens

- Tier lines from DOC-08 §10: `>=1280` desktop (full sidebar, 12-col grid, full tables);
  `768–1279` tablet (collapsed sidebar rail 64–72px + drawer, 1–2 columns, filter drawer);
  `<768` mobile (non-regression only, operational subset, direct to desktop/tablet).
- Sidebar expanded 236–248px (current 15rem/240px retained).
- Content max-width guard to keep table text within ~65–75ch where applicable; page grid 12 cols.

## 10. Source reconciliation notes

- All DOC-08 color/type tokens are adopted verbatim; no new hex introduced in the plan.
- UX Pro Max style #1 Minimalism & Swiss (enterprise/dashboards) and Data-Dense Dashboard
  corroborate DOC-08 density; its neon/dark/executive styles are rejected for this direction.
- UX Pro Max typography pairings require Google Fonts downloads, which violate repository policy;
  they are documented and rejected in favor of the approved system stack (D-005).
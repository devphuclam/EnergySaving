# Phase 3 Verification — Configuration Management

Feature: `004-industrial-operations-ui-ux-redesign`  
Scope: T037–T047 only  
Baseline: `fa3355e88bca16daeefee009922701738a4cb499`  
Branch: `feat/004-phase-03-configuration-management`

## Contract inventory gate

This inventory was completed before production implementation. It is read-only evidence of the
existing frontend gateway and server contract; it does not introduce a new API shape.

| Capability | Classification | Evidence / boundary |
|---|---|---|
| Management list search | AVAILABLE_DIRECTLY | `ManagementFilter.search`; gateway forwards `search`. |
| Management list status filter | AVAILABLE_DIRECTLY | `ManagementFilter.status`; gateway forwards `status`. |
| Management list Site filter | AVAILABLE_DIRECTLY | `ManagementFilter.siteId`; gateway forwards `siteId`. |
| Management list Area filter | AVAILABLE_DIRECTLY | `ManagementFilter.areaId`; gateway forwards `areaId`. |
| Page and page size | AVAILABLE_DIRECTLY | `ManagementFilter.page/pageSize`; gateway forwards both. |
| Server-wide sort | ABSENT_FROM_EXISTING_CONTRACT | No sort field/direction in `ManagementFilter`; UI must label sorting as current-page only. |
| Lifecycle action | AVAILABLE_DIRECTLY | `ManagementGateway.lifecycle(resource,id,action,expectedVersion,retryKey)` with `If-Match`. |
| Destructive confirmation | PAGE_LOCAL_PRESENTATION_ONLY | Confirmation is a client safety gate; mutation remains server-owned. |
| Persisted lifecycle reason | ABSENT_FROM_EXISTING_CONTRACT | No reason field/argument is represented; do not collect an unpersisted reason. |
| Update optimistic version | AVAILABLE_DIRECTLY | `ManagementGateway.update` and `If-Match`. |
| Delete optimistic version | AVAILABLE_DIRECTLY | `ManagementGateway.remove` and `If-Match`. |
| Duplicate target Source | AVAILABLE_DIRECTLY | Existing `duplicate(..., targetSourceId)` mapping. |
| Simulator review receipt | AVAILABLE_DIRECTLY | Existing `reviewSimulatorConfiguration` contract. |
| Simulator validation receipt | AVAILABLE_DIRECTLY | Existing `validate` contract and response flags. |
| Simulator version activation | AVAILABLE_DIRECTLY | Existing `activateSimulatorConfigurationVersion` contract. |
| Entity detail | AVAILABLE_DIRECTLY | Existing `detail(resource,id)` contract. |
| Effective authorization | BLOCKED_BY_EXISTING_CONTRACT | Server remains enforcement point; no client permission inference is added. |
| Server-side lifecycle reason persistence | ABSENT_FROM_EXISTING_CONTRACT | `DEFERRED_EXTERNAL_CONTRACT_LIMITATION`; Phase 3 does not invent payload fields. |

## UI UX Pro Max supporting runtime

The installed runtime was attempted with:

```text
python --version
python .agents/skills/ui-ux-pro-max/scripts/search.py "industrial configuration management enterprise operations compact tables forms lifecycle accessibility" --design-system -f markdown -p "IUMP Configuration"
```

Result: `BLOCKED_BY_MISSING_TOOL` — Windows resolves `python.exe` to the Microsoft Store alias and
no runnable Python interpreter is available. No package, font, icon or external download was
attempted. The previously recorded Phase 0 search output remains supporting intelligence only;
DOC-08, Feature 004 contracts and repository policy remain authoritative. Rejected guidance for
this phase includes dark/glass/marketing layouts, oversized typography, new packages, and any
server-wide sort or lifecycle-reason claim not present in the contract.

## T037 red evidence

`src/Web/src/test/configuration-red-evidence.test.tsx` was created before the production migration
and initially imported the planned configuration contract exports. The preimplementation source
check was therefore intentionally **RED / expected compile failure** until T038–T043 supplied the
exports. Runtime execution is `NOT_RUN` because no approved frontend behavior runner exists.

## Evidence ledger

| Task | Result | Evidence |
|---|---|---|
| T037 | PASS (source red/green) | Red evidence file created before migration; final source checks type-check. |
| T038 | PASS (source-visible) | Shared `DataTable`, `FilterBar`, `Pagination`; compact CSS; current-page-only sort label. |
| T039 | PASS (source-visible) | Sites/Areas/Assets tabs use the shared list, detail drawer, Draft form and state seams. |
| T040 | PASS (source-visible) | Points/Data Sources/Source Mappings use explicit authorized selectors and dependency states. |
| T041 | PASS (source-visible) | Simulator Configurations remain a management tab only; no Simulator workspace behavior added. |
| T042 | PASS (source-visible) | Shared forms, error summary/focus, dirty guard and destructive confirmation; no reason input. |
| T043 | PASS (source-visible) | Shared feedback, loading, empty, error, forbidden, conflict and blocked state owners consumed. |
| T044 | PASS / NOT_RUN split | Build and lint PASS; exact source checks type-check; frontend runtime runner NOT_RUN/BLOCKED_BY_PACKAGE_POLICY. |
| T045 | PASS | Standards review: 0 Critical / 0 High / 0 actionable Medium. |
| T046 | PASS | Specification review: all seven entities and the requested FR/SC trace are represented. |
| T047 | PASS | Checkpoint records completeness, limitations, readiness and explicit stop before T048. |

Frontend runtime behavior, automated browser/a11y and visual rendering remain `BLOCKED_BY_PACKAGE_POLICY`
or `NOT_RUN` unless actual approved evidence is available; they are never promoted to PASS.

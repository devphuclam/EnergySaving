# Environment Inventory

Inventory date: 2026-07-23. Inspection was read-only; no restore, install, download, registry
change, PATH change, service change, or administrator operation was performed.

| Tool/capability | Version/path/source | Internet needed by configured source | Status | Evidence/blocker |
|---|---|---:|---|---|
| Git | 2.54.0.windows.1; installed executable | No | AVAILABLE_AND_APPROVED | `git --version`; `where.exe git` |
| .NET SDK | 10.0.300; `C:\Program Files\dotnet\dotnet.exe` | No for the package-free R0 graph | AVAILABLE_AND_APPROVED | `dotnet --info`; pinned `global.json`; no-source restore/build evidence |
| Node.js | v24.16.0; `C:\Users\TD-999\Notes\node.exe` | No for direct execution | AVAILABLE_AND_APPROVED | `node --version`; `where.exe node` |
| npm | 11.13.0 | Yes: `https://registry.npmjs.org/` | BLOCKED_BY_POLICY for install | `npm --version`; `npm config get registry`; `npm config list` |
| PostgreSQL client | No `psql` executable | N/A | MISSING | `where.exe psql`; `Get-Command psql`; `psql --version` |
| PostgreSQL database | No approved local/internal endpoint supplied | N/A | BLOCKED_BY_POLICY | Mode C; no network scan or credential inference performed |
| NuGet user configuration | `nuget.org` plus Visual Studio offline packages | Yes for nuget.org | BLOCKED_BY_POLICY for future package restore | `dotnet nuget list source`; user `NuGet.Config` inspection |
| NuGet R0 configuration | Repository `NuGet.Config` contains `<clear />`; R0 projects have zero PackageReference entries | No | AVAILABLE_AND_APPROVED for current R0 graph | `dotnet nuget list source --configfile .\NuGet.Config` returned “No sources found”; restore/build passed |
| npm cache | `C:\Users\TD-999\AppData\Local\npm-cache` | No if complete | AVAILABLE_BUT_SOURCE_UNVERIFIED | Lock has 82 entries; installed tree lacks 49 lock entries, many optional/platform packages |
| Spec Kit | 0.13.2; project `.specify/` and `.agents/skills/speckit-*` | No | AVAILABLE_AND_APPROVED | `specify --version`; local skill inventory |
| Matt Pocock Skills | Project-local `.agents/skills/` | No | AVAILABLE_AND_APPROVED | required skill directories and `SKILL.md` files inspected |
| CI runner/actions | Existing workflow uses public actions and a container service | Yes/downloads actions and image | BLOCKED_BY_POLICY | `.github/workflows/ci.yml` inspection; no approved runner/template supplied |

## Dependency readiness

- Initial cache inspection found two missing preview packages in a duplicate non-solution project.
  That duplicate and all unapproved package references were removed from the R0 graph rather than
  restored or version-changed.
- Current R0 backend projects have no PackageReference entries. A restore using the repository
  no-source configuration succeeded using installed framework packs, followed by a 13-project
  Release build of 17 projects with zero warnings/errors. Future package additions remain blocked until a company
  mirror is supplied.
- `src/Web/node_modules` exists. The lock/tree comparison included optional cross-platform packages,
  so a clean offline install is not certified; however direct lint and production build against the
  installed tree passed without install or network access.

## Classification rule

`AVAILABLE_AND_APPROVED` means direct execution is allowed under the request. It does not authorize
network package resolution. `AVAILABLE_BUT_SOURCE_UNVERIFIED` requires company confirmation before
restore/install. `MISSING` and `BLOCKED_BY_POLICY` stop affected verification only.

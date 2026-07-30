# Ordered PostgreSQL Migration Evidence

Date: 2026-07-30  
Baseline: `9d03895a6c82e596223bb1a846f9e8888ecdd9dd`  
Target: approved local PostgreSQL 18 at `127.0.0.1:5433/iump_dev`

## Result

T233: **PASS**.

- PostgreSQL 18 client used from the approved absolute installation path.
- Fail-fast behavior: `ON_ERROR_STOP=1`.
- Clean isolated temporary database, migrations 0001-0013 in order: **13/13 PASS**, exit 0.
- Isolated N-1 database, migrations 0001-0012 followed by 0013: **PASS**, exit 0.
- Only temporary databases created by this closure were dropped.
- Existing `iump_dev` was inspected and was not dropped.
- `iump_dev` migration level: **13**, schemas: **8**, tables: **31**.
- Bundled `btree_gist`: **PASS**, version 1.8; no extension download.
- `0013_r1_validation_reconciliation.sql` rerun as `iump_migration`: **PASS**, exit 0.
- Database mutation: **YES** — approved roles, migrations, deterministic local bootstrap
  fixtures, and isolated runtime-verification rows were written to `iump_dev`.
- Prohibited port `5432` contacted: **NO**.

## Ordered SHA-256 evidence

| Migration | SHA-256 |
|---|---|
| 0001_r0_foundation.sql | `C55B809A027A974040F08AC87B65B2FC8625276AAF033279C02200FBB1551C0E` |
| 0002_iam_foundation.sql | `1E823103BC47880F7B8E4089DB803CB923F397A07763C684FEF35A2F9691FFF4` |
| 0003_catalog_foundation.sql | `142ADE23E72E2C3CA6AC739E5E81DA502ABE386E17D8770B4AFAA6E2F32F2A7F` |
| 0004_organization_hierarchy.sql | `F51C0D1B4C3916E64B80F64CFF022836461F4EE67BD409A1B7FA9F44BED79ACB` |
| 0005_acquisition_configuration.sql | `D52B08809F0568D37A4966F52DAF97FDC40D4C26922483B7C2F2651ADA54AFF9` |
| 0006_catalog_source_mapping.sql | `7470E4D820C01F02F4D99300F3EB143621DFDC605FFDF5F771BEFCE491B77841` |
| 0007_acquisition_run.sql | `05798B048DED2BD837D5A6485DAD1F42D8DE3E0927696F48FB6DD8D7426D12E3` |
| 0008_telemetry_measurement.sql | `E8EF69D44F132F212E52E3402BD180832A51A538A0FD6F4C714F6133229A9085` |
| 0009_telemetry_latest_status.sql | `5D1D5BC56F00A7E380653B0C1663DC0F20869B9E87DE28AEA1F1DB93DFBC5251` |
| 0010_audit_event.sql | `0CE7CFB49E2D4B30DCF9774BD6337EB9318E69872C22B7B927518BFCD00D2467` |
| 0011_r1_infrastructure_expand.sql | `6F75686702DE0DB181EF85C514FF4A70E913E51783A8953C76C8C4767DBDC08D` |
| 0012_r1_idempotent_seeds.sql | `32AE4CD77698CA2EFA634491855FE7D9578D1918C565B53C844DD94B6782C486` |
| 0013_r1_validation_reconciliation.sql | `E7B1C7BE30D37A027A85A1B54B065E3588FAABA8665C079C620818C6468E172B` |

No credential or unredacted connection string is recorded in this evidence.

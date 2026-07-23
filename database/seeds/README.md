# R0 Seeds

R0 defines no business seed data. An idempotent synthetic seed will be added only when an approved
PostgreSQL instance and the IAM/site/catalog baseline are authorized. `scripts/db-seed.ps1` therefore
reports `NOT_RUN` when this directory contains no `.sql` file.

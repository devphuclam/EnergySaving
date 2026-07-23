# Restricted Local Development Runbook

1. Read `docs/environment-inventory.md` and confirm no source/approval changed.
2. Run `scripts/test.ps1`, then `scripts/build.ps1`.
3. Run `scripts/verify.ps1`; exit 20 is expected while PostgreSQL/CI approvals are missing.
4. Use `scripts/start-api.ps1`, `start-worker.ps1`, or DB scripts only after the required approved
   environment variable/service profile exists. Scripts never print secret values.
5. On failure, capture command, exit code, and redacted output. Do not install a workaround or use a
   database substitute.
6. Stop after R0; do not begin R1/VS-01.

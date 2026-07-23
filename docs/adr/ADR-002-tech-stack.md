# ADR-002: Installed React/TypeScript and ASP.NET Core Baseline

**Status:** Accepted for R0 source; dependency execution restricted  
**Date:** 2026-07-23  
**Reference:** DOC-05 §10; current environment inventory

Use React/TypeScript for the Web shell and ASP.NET Core/.NET 10 for separate API and Worker processes,
pinned to installed SDK 10.0.300. PostgreSQL access will use Npgsql/EF Core only after locked packages
are available from an approved source. R0 executable tests use installed PowerShell and framework
facilities; real PostgreSQL tests follow approved access. FluentAssertions and Testcontainers are not
part of this baseline, and container-backed testing is prohibited on the current workstation.

# ADR-015: Ordered PostgreSQL Migration Source and Expand-Contract

**Status:** Proposed; execution blocked pending approved PostgreSQL  
**Date:** 2026-07-23  
**Reference:** DOC-06 §29; DOC-07 §18

R0 stores monotonically ordered, reviewable PostgreSQL migration source and applies it through a
non-installing script using an approved `psql` service profile. Schema changes follow expand-contract;
applied shared migrations are immutable, and long backfills become idempotent observable jobs. The
long-term generation/orchestration tool (including possible EF Core migrations) remains subject to
approved packages and database validation; R0 SQL is not claimed as a production migration baseline.

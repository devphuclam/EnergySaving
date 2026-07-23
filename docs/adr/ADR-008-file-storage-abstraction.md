# ADR-008: File Storage Abstraction

**Status:** Accepted  
**Date:** 2026-07-23  
**Deciders:** Software Architect / Tech Lead  
**Reference:** DOC-05 §16, DOC-06 §20  

## Context
MVP needs to store report artifacts (PDF/CSV), evidence files, and imported CSV files. Storage location may change (local disk → network share → object storage).

## Decision
Use an `IFileStorage` abstraction with:
- Local file system implementation for MVP (configurable base path)
- Interface supporting read, write, delete, exists, and URL generation
- Files stored in date-partitioned directories
- Metadata tracked in the database (path, size, hash, created, expires)

## Consequences
- Easy migration to Azure Blob / AWS S3 / MinIO later
- No vendor lock-in
- Local implementation sufficient for on-premise MVP
- File cleanup handled by retention job

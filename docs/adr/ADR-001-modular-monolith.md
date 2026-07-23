# ADR-001: Modular Monolith Central Application

**Status:** Accepted  
**Date:** 2026-07-23  
**Reference:** DOC-05 §9; DOC-02 §10

IUMP is one modular-monolith product and release boundary because a small team needs transactional
consistency and low operational overhead. API and Worker remain separate processes and composition
roots, while modules expose contracts and own their implementation/schema writes. Microservices are
deferred until load, team ownership, or isolation evidence justifies extraction.

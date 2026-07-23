# ADR-011: Observability APIs and Export Strategy

**Status:** Accepted  
**Date:** 2026-07-23  
**Deciders:** Software Architect / Tech Lead  
**Reference:** DOC-05 §22, DOC-07 §8.1, DOC-02 §12  

## Context
MVP must support monitoring, debugging, and alerting for operations. Unknown which observability platform will be used.

## Decision
Use structured logging, health checks, and metrics:
- **Logging:** Structured JSON via `ILogger` (Serilog or built-in) — correlation ID, component, event type, duration
- **Health checks:** ASP.NET Core Health Checks with DB ping, queue depth, disk space
- **Metrics:** OpenTelemetry-compatible metrics (request count, duration, error rate, job backlog)
- Export via configurable sink (console, file, or OpenTelemetry exporter)
- No APM vendor lock-in — standard APIs only

## Consequences
- Operations can connect any OpenTelemetry-compatible backend
- Correlation IDs enable request tracing across API and Worker
- Health check endpoint for load balancer / monitoring
- Metrics enable capacity planning and SLA tracking

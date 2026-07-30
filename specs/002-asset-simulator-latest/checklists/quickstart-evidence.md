# API / Worker / Web Quickstart Evidence

Date: 2026-07-30  
Baseline: `9d03895a6c82e596223bb1a846f9e8888ecdd9dd`

## Result

T234: **PASS** against the approved PostgreSQL target
`127.0.0.1:5433/iump_dev`.

## Runtime smoke

- API startup: PASS.
- `/health/live`: HTTP 200.
- `/health/ready`: HTTP 200; database `iump_dev`, port 5433, migration level 13.
- Worker startup and PostgreSQL production/outbox loop: PASS; stderr error count 0.
- Web startup: HTTP 200.
- API login and `/api/v1/me`: HTTP 200; Administrator identity returned.
- Web login: PASS.
- HTTP command mutation: HTTP 201.

## Accepted functional journey

The real PostgreSQL runner completed the following sequence with exit 0:

1. Administrator created a Site and assigned Engineer Site scope.
2. Engineer created Area, Asset, Point, Source, Simulator configuration, and Mapping.
3. Site, Area, Asset, Source, Mapping, and Point were activated in the required order.
4. Simulator Start committed a Run.
5. Worker production finalized attempts through Telemetry.
6. Accepted Measurement persisted; Latest advanced; Source Health became Online.
7. Audit append/query evidence was visible.

Observed result:

```text
site=PASS engineer_scope=PASS area=PASS asset=PASS point=PASS source=PASS
configuration=True mapping=PASS activation=PASS simulator=PASS
latest=PASS health=PASS audit=PASS
```

The Web gateway then read PostgreSQL-backed data through the API and displayed a numeric Latest
value, `Good` quality, source/received UTC timestamps, and `Online` health. Provider-neutral
acceptance tests continue to verify that No Data has no numeric value and is never represented as
zero.

No missing CLI/tool classification remains. No secret was emitted, and port 5432 was not
contacted.

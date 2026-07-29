export function AuditRoute() {
  return (
    <section className="page" aria-labelledby="audit-title">
      <div className="page-heading"><div><p className="eyebrow">Evidence</p><h1 id="audit-title">Audit review</h1><p className="lede">Immutable configuration and control evidence for authorized reviewers.</p></div><span className="badge badge-neutral">AUDIT_READ</span></div>
      <div className="notice notice-warning"><strong>Audit review is scope-gated.</strong><span>Administrator has global access. Other users need AUDIT_READ and an explicit Site or Area scope.</span></div>
      <div className="card"><div className="card-header"><div><p className="card-kicker">Recent activity</p><h2>Waiting for delivered events</h2></div><span className="muted">Keyset order · newest first</span></div><div className="empty-state"><span className="empty-icon" aria-hidden="true">✓</span><p>No audit events in this scope yet.</p><small>Events appear after dispatch, inbox deduplication, and append complete.</small></div></div>
    </section>
  )
}

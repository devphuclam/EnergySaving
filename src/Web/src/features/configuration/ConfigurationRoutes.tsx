import { useState } from 'react'

export function ConfigurationRoutes() {
  const [saved, setSaved] = useState(false)
  return (
    <section className="page" aria-labelledby="configuration-title">
      <div className="page-heading"><div><p className="eyebrow">Workspace setup</p><h1 id="configuration-title">Configuration</h1><p className="lede">Prepare a scoped hierarchy and a versioned simulator configuration.</p></div><span className="badge badge-neutral">Draft</span></div>
      <div className="card-grid three-up">
        <article className="card"><p className="card-kicker">01 · Hierarchy</p><h2>POC Site</h2><p className="muted">Site → Area → Asset → Measurement Point</p><button className="button button-secondary" type="button" onClick={() => setSaved(true)}>Open hierarchy</button></article>
        <article className="card"><p className="card-kicker">02 · Catalog</p><h2>Electric Power</h2><p className="muted">Canonical unit: kW · Source mapping ready</p><button className="button button-secondary" type="button">Review mapping</button></article>
        <article className="card"><p className="card-kicker">03 · Version</p><h2>Simulator V1</h2><p className="muted">Immutable configuration version 7</p><button className="button button-secondary" type="button" onClick={() => setSaved(true)}>Validate configuration</button></article>
      </div>
      {saved && <div className="notice notice-success" role="status">Configuration checks passed. Changes are still Draft until activation.</div>}
      <div className="card form-card"><div><h2>Activation readiness</h2><p className="muted">Every state change uses an idempotency key and optimistic version.</p></div><dl className="readiness-list"><div><dt>Hierarchy</dt><dd><span className="badge badge-success">Ready</span></dd></div><div><dt>Source mapping</dt><dd><span className="badge badge-success">Ready</span></dd></div><div><dt>Point status</dt><dd><span className="badge badge-warning">Draft</span></dd></div></dl></div>
    </section>
  )
}

import { useEffect, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { ConfigurationSummary, GatewayState } from '../../gateways/webGateways'

const empty: ConfigurationSummary = { state: 'loading', siteCount: 0, areaCount: 0, pointCount: 0, hierarchy: 'Loading', catalog: 'Loading', sources: 'Loading', mappings: 'Loading', activation: 'Loading' }

export function ConfigurationRoutes() {
  const gateways = useWebGateways()
  const [summary, setSummary] = useState(empty)
  const [validation, setValidation] = useState<GatewayState>('loading')
  useEffect(() => { void gateways.configuration.getSummary().then(setSummary) }, [gateways.configuration])
  async function validate() { setValidation('loading'); setValidation(await gateways.configuration.validate()) }
  const unavailable = summary.state !== 'ready'
  return (
    <section className="page" aria-labelledby="configuration-title">
      <div className="page-heading"><div><p className="eyebrow">Workspace setup</p><h1 id="configuration-title">Configuration</h1><p className="lede">Prepare a scoped hierarchy and a versioned simulator configuration.</p></div><span className="badge badge-neutral">{summary.state}</span></div>
      {unavailable ? <div className="notice notice-warning" role="status">Configuration gateway: {summary.state}. No local fallback data is shown.</div> : <div className="card-grid three-up"><article className="card"><p className="card-kicker">Hierarchy</p><h2>{summary.siteCount} sites</h2><p className="muted">{summary.hierarchy}</p><button className="button button-secondary" type="button">Open hierarchy</button></article><article className="card"><p className="card-kicker">Catalog / Source / Mapping</p><h2>{summary.areaCount} areas</h2><p className="muted">{summary.catalog} · {summary.sources} · {summary.mappings}</p><button className="button button-secondary" type="button">Review mapping</button></article><article className="card"><p className="card-kicker">Activation</p><h2>{summary.pointCount} points</h2><p className="muted">{summary.activation}</p><button className="button button-secondary" type="button" onClick={() => void validate()}>Validate and activate</button></article></div>}
      {validation !== 'loading' && <div className={`notice ${validation === 'ready' ? 'notice-success' : 'notice-warning'}`} role="status">Validation: {validation}.</div>}
      <div className="card form-card"><div><h2>Activation readiness</h2><p className="muted">Every state change uses an idempotency key and optimistic version.</p></div><dl className="readiness-list"><div><dt>Hierarchy</dt><dd>{summary.state}</dd></div><div><dt>Source mapping</dt><dd>{summary.state}</dd></div><div><dt>Point status</dt><dd>{summary.state}</dd></div></dl></div>
    </section>
  )
}

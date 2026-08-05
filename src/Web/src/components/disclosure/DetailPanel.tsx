import type { ReactNode } from 'react'

export function DetailPanel({ title, description, children, action }: { title: string; description?: string; children: ReactNode; action?: ReactNode }) {
  return <section className="detail-panel" aria-labelledby="detail-panel-title"><div className="card-header"><div><h2 id="detail-panel-title">{title}</h2>{description && <p className="muted">{description}</p>}</div>{action}</div>{children}</section>
}

import type { ReactNode } from 'react'

export function EmptyState({ title = 'Chưa có dữ liệu', message, action }: { title?: string; message: string; action?: ReactNode }) {
  return <div className="state-panel state-empty" role="status"><span className="state-cue" aria-hidden="true">—</span><div><h2>{title}</h2><p>{message}</p>{action && <div className="state-action">{action}</div>}</div></div>
}

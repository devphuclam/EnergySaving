import type { ReactNode } from 'react'

export type FeedbackTone = 'info' | 'success' | 'warning' | 'danger'

export function FeedbackBanner({ tone = 'info', title, message, action, correlationId, live = true }: {
  tone?: FeedbackTone
  title?: string
  message: string
  action?: ReactNode
  correlationId?: string
  live?: boolean
}) {
  return <div className={`feedback-banner feedback-${tone}`} role={tone === 'danger' ? 'alert' : live ? 'status' : undefined} aria-live={live ? 'polite' : undefined}>
    <span className="feedback-cue" aria-hidden="true">{tone === 'success' ? '✓' : tone === 'danger' ? '!' : tone === 'warning' ? '!' : 'i'}</span>
    <div><strong>{title}</strong><p>{message}</p>{correlationId && <small className="metadata">Mã tương quan: {correlationId}</small>}</div>{action && <div className="feedback-action">{action}</div>}
  </div>
}

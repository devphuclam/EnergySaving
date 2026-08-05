import type { ReactNode } from 'react'

export function RetryState({ message = 'Có thể thử lại mà không mất ngữ cảnh.', onRetry, children }: { message?: string; onRetry?: () => void; children?: ReactNode }) {
  return <div className="state-panel state-warning" role="alert"><span className="state-cue" aria-hidden="true">↻</span><div><h2>Có thể thử lại</h2><p>{message}</p><div className="state-action">{children}{onRetry && <button className="button button-secondary" type="button" onClick={onRetry}>Thử lại</button>}</div></div></div>
}

import type { ReactNode } from 'react'

export function ErrorState({ message = 'Không thể tải dữ liệu trong phạm vi hiện tại.', action }: { message?: string; action?: ReactNode }) {
  return <div className="state-panel state-error" role="alert"><span className="state-cue" aria-hidden="true">!</span><div><h2>Không thể tải</h2><p>{message}</p>{action && <div className="state-action">{action}</div>}</div></div>
}

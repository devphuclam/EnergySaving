import type { ReactNode } from 'react'

export function ConflictState({ message = 'Dữ liệu đã thay đổi bởi một phiên khác. Không ghi đè im lặng.', action }: { message?: string; action?: ReactNode }) {
  return <div className="state-panel state-warning" role="alert"><span className="state-cue" aria-hidden="true">↻</span><div><h2>Có xung đột</h2><p>{message}</p>{action && <div className="state-action">{action}</div>}</div></div>
}

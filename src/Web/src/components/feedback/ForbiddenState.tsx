import type { ReactNode } from 'react'

export function ForbiddenState({ message = 'Nội dung này không nằm trong phạm vi được cấp.', action, title = 'Không được phép' }: { message?: string; action?: ReactNode; title?: string }) {
  return <div className="state-panel state-forbidden" role="alert"><span className="state-cue" aria-hidden="true">⊘</span><div><h2>{title}</h2><p>{message}</p>{action && <div className="state-action">{action}</div>}</div></div>
}

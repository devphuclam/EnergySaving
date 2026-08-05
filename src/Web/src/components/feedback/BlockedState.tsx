import type { ReactNode } from 'react'

export function BlockedState({ message, nextAction }: { message: string; nextAction?: ReactNode }) {
  return <div className="state-panel state-warning" role="alert"><span className="state-cue" aria-hidden="true">⊘</span><div><h2>Bị chặn</h2><p>{message}</p>{nextAction && <div className="state-action">{nextAction}</div>}</div></div>
}

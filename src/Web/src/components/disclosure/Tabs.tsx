import { useState, type KeyboardEvent, type ReactNode } from 'react'

export type TabItem = { id: string; label: string; content: ReactNode }

export function Tabs({ items, initialId }: { items: readonly TabItem[]; initialId?: string }) {
  const [active, setActive] = useState(initialId ?? items[0]?.id ?? '')
  function move(event: KeyboardEvent<HTMLButtonElement>, index: number) {
    if (!['ArrowRight', 'ArrowLeft', 'Home', 'End'].includes(event.key)) return
    event.preventDefault()
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? items.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : -1) + items.length) % items.length
    setActive(items[next].id)
    document.getElementById(`tab-${items[next].id}`)?.focus()
  }
  const current = items.find(item => item.id === active) ?? items[0]
  return <div className="tabs-component"><div className="tabs" role="tablist" aria-label="Các phần chi tiết">{items.map((item, index) => <button id={`tab-${item.id}`} className={`tab${item.id === current?.id ? ' tab-active' : ''}`} type="button" role="tab" aria-selected={item.id === current?.id} aria-controls={`panel-${item.id}`} tabIndex={item.id === current?.id ? 0 : -1} key={item.id} onClick={() => setActive(item.id)} onKeyDown={event => move(event, index)}>{item.label}</button>)}</div>{current && <section id={`panel-${current.id}`} role="tabpanel" tabIndex={0} aria-labelledby={`tab-${current.id}`}>{current.content}</section>}</div>
}

import type { FormEvent, ReactNode } from 'react'

export type FilterDefinition = { id: string; label: string; value: string; placeholder?: string; type?: 'text' | 'search' | 'date' }

export function FilterBar({ fields, onChange, onSubmit, onReset, resultCount, children }: {
  fields: readonly FilterDefinition[]
  onChange: (id: string, value: string) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onReset?: () => void
  resultCount?: number
  children?: ReactNode
}) {
  return <form className="filter-bar" onSubmit={onSubmit} aria-label="Bộ lọc dữ liệu">
    {fields.map(field => <label className="field" htmlFor={`filter-${field.id}`} key={field.id}>{field.label}<input id={`filter-${field.id}`} className="input" type={field.type ?? 'text'} value={field.value} placeholder={field.placeholder} onChange={event => onChange(field.id, event.target.value)} /></label>)}
    <div className="filter-actions"><button className="button button-primary" type="submit">Áp dụng</button>{onReset && <button className="button button-secondary" type="button" onClick={onReset}>Xóa bộ lọc</button>}{children}</div>
    {resultCount !== undefined && <span className="filter-count" role="status">{resultCount} kết quả</span>}
  </form>
}

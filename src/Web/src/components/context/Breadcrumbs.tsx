import type { ReactNode } from 'react'

export type Breadcrumb = { label: string; href?: string }

export function Breadcrumbs({ items }: { items: readonly Breadcrumb[] }) {
  if (items.length === 0) return null
  return <nav className="breadcrumbs" aria-label="Breadcrumb">
    <ol>{items.map((item, index) => <li key={`${item.label}-${index}`}>
      {item.href && index < items.length - 1 ? <a href={item.href}>{item.label}</a> : <span aria-current={index === items.length - 1 ? 'page' : undefined}>{item.label}</span>}
    </li>)}</ol>
  </nav>
}

export function BreadcrumbTrail({ children }: { children: ReactNode }) {
  return <div className="breadcrumbs">{children}</div>
}

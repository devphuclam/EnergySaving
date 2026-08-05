import type { ReactNode } from 'react'
import { Breadcrumbs, type Breadcrumb } from './Breadcrumbs'

export function PageHeader({ eyebrow, title, titleId, description, breadcrumbs, primaryAction, secondaryActions }: {
  eyebrow?: string
  title: string
  titleId?: string
  description?: string
  breadcrumbs?: readonly Breadcrumb[]
  primaryAction?: ReactNode
  secondaryActions?: ReactNode
}) {
  return <header className="page-header">
    {breadcrumbs && <Breadcrumbs items={breadcrumbs} />}
    <div className="page-header-row"><div>
      {eyebrow && <p className="eyebrow">{eyebrow}</p>}
      <h1 id={titleId} data-route-title tabIndex={-1}>{title}</h1>
      {description && <p className="lede">{description}</p>}
    </div><div className="page-actions">{secondaryActions}{primaryAction}</div></div>
  </header>
}

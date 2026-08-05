import type { ReactNode } from 'react'

export type DataTableColumn<T> = { key: string; header: string; render: (row: T) => ReactNode; numeric?: boolean }

export function DataTable<T>({ columns, rows, rowKey, caption, emptyMessage = 'Không có bản ghi trong phạm vi hoặc bộ lọc hiện tại.', rowAction }: {
  columns: readonly DataTableColumn<T>[]
  rows: readonly T[]
  rowKey: (row: T, index: number) => string
  caption: string
  emptyMessage?: string
  rowAction?: (row: T) => ReactNode
}) {
  if (rows.length === 0) return <div className="table-empty" role="status"><span className="state-cue" aria-hidden="true">—</span><p>{emptyMessage}</p></div>
  return <div className="table-scroll" tabIndex={0} aria-label={`${caption}, có thể cuộn ngang trên tablet`}>
    <table className="data-table"><caption>{caption}</caption><thead><tr>{columns.map(column => <th scope="col" key={column.key} className={column.numeric ? 'numeric' : undefined}>{column.header}</th>)}{rowAction && <th scope="col">Thao tác</th>}</tr></thead>
      <tbody>{rows.map((row, index) => <tr key={rowKey(row, index)}>{columns.map(column => <td key={column.key} className={column.numeric ? 'numeric' : undefined}>{column.render(row)}</td>)}{rowAction && <td className="actions-cell">{rowAction(row)}</td>}</tr>)}</tbody>
    </table>
  </div>
}

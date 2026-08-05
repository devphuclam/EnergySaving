export function Pagination({ page, pageSize, total, onPageChange }: { page: number; pageSize: number; total: number; onPageChange: (page: number) => void }) {
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  return <nav className="pagination" aria-label="Phân trang">
    <button className="button button-secondary" type="button" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>Trang trước</button>
    <span aria-current="page">Trang {page} / {pageCount}</span>
    <button className="button button-secondary" type="button" disabled={page >= pageCount} onClick={() => onPageChange(page + 1)}>Trang sau</button>
  </nav>
}

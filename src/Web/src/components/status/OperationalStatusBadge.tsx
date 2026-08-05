export type OperationalStatus = 'Good' | 'Uncertain' | 'Bad' | 'Missing' | 'Stale' | 'Blocked' | 'Forbidden' | 'Conflict' | 'Pending' | 'Processing' | 'CompletedWithErrors' | 'Retryable' | 'Available' | 'Unavailable'

const statusCopy: Record<OperationalStatus, { label: string; cue: string; tone: string }> = {
  Good: { label: 'Tốt', cue: '✓', tone: 'success' }, Uncertain: { label: 'Không chắc chắn', cue: '!', tone: 'warning' },
  Bad: { label: 'Xấu', cue: '×', tone: 'danger' }, Missing: { label: 'Không có dữ liệu', cue: '—', tone: 'missing' },
  Stale: { label: 'Cũ (stale)', cue: '◷', tone: 'warning' }, Blocked: { label: 'Bị chặn', cue: '⊘', tone: 'warning' },
  Forbidden: { label: 'Không được phép', cue: '⊘', tone: 'neutral' }, Conflict: { label: 'Có xung đột', cue: '↻', tone: 'warning' },
  Pending: { label: 'Đang chờ', cue: '…', tone: 'primary' }, Processing: { label: 'Đang xử lý', cue: '…', tone: 'primary' },
  CompletedWithErrors: { label: 'Hoàn tất có lỗi', cue: '!', tone: 'warning' }, Retryable: { label: 'Có thể thử lại', cue: '↻', tone: 'warning' },
  Available: { label: 'Sẵn sàng', cue: '✓', tone: 'success' }, Unavailable: { label: 'Không khả dụng', cue: '!', tone: 'danger' },
}

export function OperationalStatusBadge({ status, detail }: { status: OperationalStatus; detail?: string }) {
  const copy = statusCopy[status]
  return <span className={`status-badge status-${copy.tone}`} role="status" aria-label={`${copy.label}${detail ? `: ${detail}` : ''}`}>
    <span className="status-cue" aria-hidden="true">{copy.cue}</span><span>{copy.label}</span>{detail && <small>{detail}</small>}
  </span>
}

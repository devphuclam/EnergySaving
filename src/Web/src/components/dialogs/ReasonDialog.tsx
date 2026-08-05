import { useEffect, useState } from 'react'
import { ConfirmDialog } from './ConfirmDialog'

export function ReasonDialog({ open, title, description, onConfirm, onCancel, required = true }: {
  open: boolean; title: string; description: string; onConfirm: (reason: string) => void; onCancel: () => void; required?: boolean
}) {
  const [reason, setReason] = useState('')
  useEffect(() => { if (!open) setReason('') }, [open])
  return <ConfirmDialog open={open} title={title} description={description} onCancel={onCancel} onConfirm={() => { if (!required || reason.trim()) onConfirm(reason.trim()) }} confirmLabel="Xác nhận với lý do">
    <label className="field-control" htmlFor="reason-dialog-input"><span>Lý do{required && ' *'}</span><textarea id="reason-dialog-input" value={reason} onChange={event => setReason(event.target.value)} aria-required={required} /></label>
  </ConfirmDialog>
}

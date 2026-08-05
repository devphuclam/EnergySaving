import { useEffect, useId, useRef, useState } from 'react'
import { ConfirmDialog } from './ConfirmDialog'

/** Required-reason validation: empty required reason produces visible feedback, never a silent no-op. */
export function reasonRequiredValidation(reason: string, required: boolean, attempted: boolean): string | undefined {
  if (!required || !attempted) return undefined
  return reason.trim() ? undefined : 'Lý do là bắt buộc.'
}

export function ReasonDialog({ open, title, description, onConfirm, onCancel, required = true }: {
  open: boolean; title: string; description: string; onConfirm: (reason: string) => void; onCancel: () => void; required?: boolean
}) {
  const [reason, setReason] = useState('')
  const [attempted, setAttempted] = useState(false)
  const inputId = useId()
  const errorId = useId()
  const inputRef = useRef<HTMLTextAreaElement>(null)
  useEffect(() => { if (!open) { setReason(''); setAttempted(false) } }, [open])
  const error = reasonRequiredValidation(reason, required, attempted)
  return <ConfirmDialog open={open} title={title} description={description} onCancel={onCancel} onConfirm={() => {
    if (error) {
      setAttempted(true)
      inputRef.current?.focus()
      return
    }
    onConfirm(reason.trim())
  }} confirmLabel="Xác nhận với lý do">
    <div className="field-control">
      <label htmlFor={inputId}><span>Lý do{required && ' *'}</span></label>
      <textarea id={inputId} ref={inputRef} value={reason} onChange={event => setReason(event.target.value)} aria-required={required} aria-invalid={error ? true : undefined} aria-describedby={error ? errorId : undefined} />
      {error && <small id={errorId} className="field-error" role="alert">{error}</small>}
    </div>
  </ConfirmDialog>
}

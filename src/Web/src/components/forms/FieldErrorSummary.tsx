import { useEffect, useId, useRef } from 'react'

export type FieldError = { id: string; label: string; message: string }

/**
 * Deterministic focus decision for a failed submit: the first invalid field when one exists, the
 * summary itself when no field id is available, and nothing when there is nothing to report.
 */
export function fieldErrorSummaryFocusTarget(errors: readonly FieldError[]): string | 'summary' | undefined {
  if (errors.length === 0) return undefined
  return firstErrorFieldId(errors) ?? 'summary'
}

/**
 * Explicit activation contract: the consumer increments `activationKey` on every submit attempt.
 * Mounting with pre-existing server errors never forces focus (key 0). Every new key re-evaluates
 * the errors, so repeated submits with remaining invalid fields keep moving focus deterministically
 * to the first invalid field (or to the summary when the field is not focusable).
 */
export function firstErrorFieldId(errors: readonly FieldError[]): string | undefined {
  return errors[0]?.id
}

export function FieldErrorSummary({ errors, activationKey = 0 }: { errors: readonly FieldError[]; activationKey?: number }) {
  const titleId = useId()
  const summaryRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (activationKey <= 0 || errors.length === 0) return
    const target = fieldErrorSummaryFocusTarget(errors)
    if (!target) return
    if (target === 'summary') {
      summaryRef.current?.focus({ preventScroll: false })
      return
    }
    const field = document.getElementById(target)
    if (field && 'focus' in field) field.focus({ preventScroll: false })
    else summaryRef.current?.focus({ preventScroll: false })
  }, [activationKey, errors])
  if (errors.length === 0) return null
  return <div ref={summaryRef} tabIndex={-1} className="field-error-summary" role="alert" aria-labelledby={titleId}><strong id={titleId}>Kiểm tra các trường bắt buộc</strong><ul>{errors.map(error => <li key={error.id}><a href={`#${error.id}`}>{error.label}: {error.message}</a></li>)}</ul></div>
}

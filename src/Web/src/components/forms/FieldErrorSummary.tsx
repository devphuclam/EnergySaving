import { useEffect, useId, useRef } from 'react'

export type FieldError = { id: string; label: string; message: string }

/**
 * Explicit activation flag: the consumer flips `activate` to true only after a submit attempt.
 * Mounting with pre-existing server errors never forces focus; focus moves deterministically to
 * the first invalid field (or to the summary when the field is not focusable) only on activation.
 */
export function firstErrorFieldId(errors: readonly FieldError[]): string | undefined {
  return errors[0]?.id
}

export function FieldErrorSummary({ errors, activate = false }: { errors: readonly FieldError[]; activate?: boolean }) {
  const titleId = useId()
  const summaryRef = useRef<HTMLDivElement>(null)
  const handledActivation = useRef(false)
  useEffect(() => {
    if (!activate || handledActivation.current) return
    handledActivation.current = true
    if (errors.length === 0) return
    const first = firstErrorFieldId(errors)
    if (!first) return
    const target = document.getElementById(first)
    if (target && 'focus' in target) target.focus({ preventScroll: false })
    else summaryRef.current?.focus({ preventScroll: false })
  }, [activate, errors])
  if (errors.length === 0) return null
  return <div ref={summaryRef} tabIndex={-1} className="field-error-summary" role="alert" aria-labelledby={titleId}><strong id={titleId}>Kiểm tra các trường bắt buộc</strong><ul>{errors.map(error => <li key={error.id}><a href={`#${error.id}`}>{error.label}: {error.message}</a></li>)}</ul></div>
}

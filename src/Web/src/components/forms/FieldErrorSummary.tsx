export type FieldError = { id: string; label: string; message: string }

export function FieldErrorSummary({ errors }: { errors: readonly FieldError[] }) {
  if (errors.length === 0) return null
  return <div className="field-error-summary" role="alert" aria-labelledby="field-error-summary-title"><strong id="field-error-summary-title">Kiểm tra các trường bắt buộc</strong><ul>{errors.map(error => <li key={error.id}><a href={`#${error.id}`}>{error.label}: {error.message}</a></li>)}</ul></div>
}

import type { InputHTMLAttributes, ReactNode } from 'react'

export function Field({ id, label, required, error, helper, children, ...inputProps }: { id: string; label: string; required?: boolean; error?: string; helper?: string; children?: ReactNode } & Omit<InputHTMLAttributes<HTMLInputElement>, 'id' | 'required'>) {
  const describedBy = [helper ? `${id}-help` : undefined, error ? `${id}-error` : undefined].filter(Boolean).join(' ') || undefined
  return <div className="field-control"><label htmlFor={id}>{label}{required && <span className="required-mark" aria-label="bắt buộc"> *</span>}</label>
    {children ?? <input id={id} {...inputProps} aria-invalid={error ? true : undefined} aria-describedby={describedBy} required={required} />}
    {helper && <small id={`${id}-help`} className="field-helper">{helper}</small>}{error && <small id={`${id}-error`} className="field-error" role="alert">{error}</small>}
  </div>
}

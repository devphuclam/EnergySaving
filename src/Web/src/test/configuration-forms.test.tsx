import { Field } from '../components/forms/Field'
import { FieldErrorSummary } from '../components/forms/FieldErrorSummary'
import { FormSection } from '../components/forms/FormSection'
import { UnsavedChangesGuard } from '../components/forms/UnsavedChangesGuard'

export function runConfigurationFormChecks(): string[] {
  return [Field, FieldErrorSummary, FormSection, UnsavedChangesGuard].every(component => typeof component === 'function') ? [] : ['form primitives must be importable']
}

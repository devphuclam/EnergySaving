import { ConfirmDialog } from '../components/dialogs/ConfirmDialog'
import { ReasonDialog } from '../components/dialogs/ReasonDialog'

export function runDialogFocusChecks(): string[] {
  return typeof ConfirmDialog === 'function' && typeof ReasonDialog === 'function' ? [] : ['dialog primitives must be importable']
}

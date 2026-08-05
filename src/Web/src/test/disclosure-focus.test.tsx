import { DetailPanel } from '../components/disclosure/DetailPanel'
import { Drawer } from '../components/disclosure/Drawer'
import { Tabs } from '../components/disclosure/Tabs'

export function runDisclosureFocusChecks(): string[] {
  return typeof Drawer === 'function' && typeof DetailPanel === 'function' && typeof Tabs === 'function' ? [] : ['disclosure primitives must be importable']
}

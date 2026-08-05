import { groupNavigationItems, visibleNavigationItems, navigationItems } from '../components/navigation/NavigationModel'

export function runNavigationChecks(): string[] {
  const failures: string[] = []
  if (groupNavigationItems(navigationItems).length < 3) failures.push('navigation must use operational groups')
  if (visibleNavigationItems({ capabilities: [] }).some(item => item.route === 'audit')) failures.push('AUDIT_READ must remain permission filtered')
  if (!visibleNavigationItems({}).some(item => item.route === 'dashboard')) failures.push('legacy session shape must retain server-enforced dashboard visibility')
  return failures
}

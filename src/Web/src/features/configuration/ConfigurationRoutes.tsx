import { ConfigurationManagementRoutes } from './ConfigurationManagementRoutes'

export function ConfigurationRoutes({ onSessionRecovery }: { onSessionRecovery?: () => void }) {
  return <ConfigurationManagementRoutes onSessionRecovery={onSessionRecovery} />
}

import { AppShell } from './app/AppShell'
import { ConfigurationRoutes } from './features/configuration/ConfigurationRoutes'
import { SimulatorRoute } from './features/simulator/SimulatorRoute'
import { PointCurrentRoute } from './features/telemetry/PointCurrentRoute'
import { AuditRoute } from './features/audit/AuditRoute'
import { OperationalDashboard } from './features/dashboard/OperationalDashboard'
import { GatewayProvider } from './gateways/GatewayContext'
import './App.css'
import { SetupWizard } from './features/setup/SetupWizard'

function App() {
  return <GatewayProvider><AppShell>{(route, navigate, session, locationKey) => route === 'setup'
    ? <SetupWizard key={locationKey} onSimulator={() => navigate('simulator')} />
    : route === 'dashboard'
      ? <OperationalDashboard session={session} onNewSetup={() => navigate('setup', { mode: 'new' })} onContinueSetup={() => navigate('setup')} onNavigate={navigate} />
      : route === 'configuration' ? <ConfigurationRoutes />
        : route === 'simulator' ? <SimulatorRoute />
          : route === 'telemetry' ? <PointCurrentRoute /> : <AuditRoute />}</AppShell></GatewayProvider>
}

export default App

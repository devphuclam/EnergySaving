import { AppShell } from './app/AppShell'
import { ConfigurationRoutes } from './features/configuration/ConfigurationRoutes'
import { SimulatorRoute } from './features/simulator/SimulatorRoute'
import { PointCurrentRoute } from './features/telemetry/PointCurrentRoute'
import { AuditRoute } from './features/audit/AuditRoute'
import './App.css'

function App() {
  return <AppShell>{(route) => route === 'configuration' ? <ConfigurationRoutes /> : route === 'simulator' ? <SimulatorRoute /> : route === 'telemetry' ? <PointCurrentRoute /> : <AuditRoute />}</AppShell>
}

export default App

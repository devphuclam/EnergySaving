import { AppShell } from './app/AppShell'
import { ConfigurationRoutes } from './features/configuration/ConfigurationRoutes'
import { SimulatorRoute } from './features/simulator/SimulatorRoute'
import { PointCurrentRoute } from './features/telemetry/PointCurrentRoute'
import { AuditRoute } from './features/audit/AuditRoute'
import { GatewayProvider } from './gateways/GatewayContext'
import './App.css'
import { SetupWizard } from './features/setup/SetupWizard'
import type { AuthSession } from './gateways/webGateways'

function OperationalDashboard({ session, onNewSetup }: { session: AuthSession; onNewSetup: () => void }) {
  return <section className="setup-card">
    <p className="eyebrow">KHÔNG GIAN VẬN HÀNH</p>
    <h1>Chuỗi cấu hình đã sẵn sàng</h1>
    <p>Chọn chức năng từ thanh điều hướng. Dashboard đầy đủ được triển khai ở Phase 5.</p>
    {session.isAdministrator && <button className="button button-primary" type="button" onClick={onNewSetup}>
      Tạo chuỗi cấu hình mới
    </button>}
  </section>
}

function App() {
  return <GatewayProvider><AppShell>{(route, navigate, session, locationKey) => route === 'setup' ? <SetupWizard key={locationKey} onSimulator={() => navigate('simulator')} /> : route === 'dashboard' ? <OperationalDashboard session={session} onNewSetup={() => navigate('setup', { mode: 'new' })} /> : route === 'configuration' ? <ConfigurationRoutes /> : route === 'simulator' ? <SimulatorRoute /> : route === 'telemetry' ? <PointCurrentRoute /> : <AuditRoute />}</AppShell></GatewayProvider>
}

export default App

import './App.css'

const checks = [
  ['Source documents', 'VERIFIED'],
  ['Tool inventory', 'VERIFIED'],
  ['PostgreSQL execution', 'BLOCKED BY ENVIRONMENT'],
  ['Company CI runner', 'REQUIRES COMPANY APPROVAL'],
]

function App() {
  return (
    <main className="shell">
      <p className="eyebrow">IDEA Utility Monitoring Platform</p>
      <h1>R0 Engineering Foundation</h1>
      <p className="summary">
        Internal decision-support foundation only. No business dashboard, equipment control,
        hardware integration, Edge production behavior, or later-release workflow is active.
      </p>
      <section aria-labelledby="readiness-heading">
        <h2 id="readiness-heading">Readiness</h2>
        <dl>
          {checks.map(([label, status]) => (
            <div className="check" key={label}>
              <dt>{label}</dt>
              <dd>{status}</dd>
            </div>
          ))}
        </dl>
      </section>
    </main>
  )
}

export default App

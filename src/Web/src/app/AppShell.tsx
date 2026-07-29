import { useState, type ReactNode } from 'react'

export type WebRoute = 'configuration' | 'simulator' | 'telemetry' | 'audit'

export type AppShellProps = {
  children: (route: WebRoute) => ReactNode
}

export function AppShell({ children }: AppShellProps) {
  const [route, setRoute] = useState<WebRoute>('configuration')
  const [authenticated, setAuthenticated] = useState(false)
  const [feedback, setFeedback] = useState('')
  const [scope, setScope] = useState('No site scope selected')

  function signIn() {
    setAuthenticated(true)
    setScope('POC Site scope')
    setFeedback('Signed in. Your authorized scope is ready.')
  }

  function signOut() {
    setAuthenticated(false)
    setScope('No site scope selected')
    setFeedback('Signed out.')
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="#configuration" onClick={() => setRoute('configuration')}>
          <span className="brand-mark" aria-hidden="true">I</span>
          <span>IDEA Utility Monitoring</span>
        </a>
        <div className="session-controls">
          <span className="scope-pill" aria-label="Current scope">{scope}</span>
          {authenticated ? (
            <button className="button button-quiet" type="button" onClick={signOut}>Sign out</button>
          ) : (
            <button className="button button-primary" type="button" onClick={signIn}>Sign in</button>
          )}
        </div>
      </header>
      <div className="layout">
        <nav className="sidebar" aria-label="Primary navigation">
          <p className="nav-heading">Workspace</p>
          {(['configuration', 'simulator', 'telemetry', 'audit'] as WebRoute[]).map((item) => (
            <button
              className={`nav-link${route === item ? ' active' : ''}`}
              type="button"
              key={item}
              aria-current={route === item ? 'page' : undefined}
              onClick={() => setRoute(item)}
            >
              {item === 'configuration' ? 'Configuration' : item === 'simulator' ? 'Simulator' : item === 'telemetry' ? 'Latest & health' : 'Audit review'}
            </button>
          ))}
          <div className="sidebar-note">
            <span className="status-dot" aria-hidden="true" />
            <span>Provider-neutral mode</span>
          </div>
        </nav>
        <main className="content" id="main-content">
          <div className="feedback" role="status" aria-live="polite">{feedback}</div>
          {!authenticated && (
            <section className="notice notice-info" aria-label="Authentication notice">
              <strong>Sign in to manage this workspace.</strong>
              <span>Queries and mutations are scope-checked server-side.</span>
            </section>
          )}
          {children(route)}
        </main>
      </div>
    </div>
  )
}

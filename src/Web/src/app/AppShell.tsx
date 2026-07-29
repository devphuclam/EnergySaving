import { useEffect, useState, type ReactNode } from 'react'
import { useWebGateways } from '../gateways/GatewayContext'
import type { AuthSession } from '../gateways/webGateways'

export type WebRoute = 'configuration' | 'simulator' | 'telemetry' | 'audit'

export type AppShellProps = {
  children: (route: WebRoute) => ReactNode
}

export function AppShell({ children }: AppShellProps) {
  const gateways = useWebGateways()
  const [route, setRoute] = useState<WebRoute>('configuration')
  const [session, setSession] = useState<AuthSession>({ state: 'loading' })
  const [feedback, setFeedback] = useState('')

  useEffect(() => {
    void gateways.auth.getSession().then(setSession)
  }, [gateways.auth])

  async function signIn() {
    const next = await gateways.auth.signIn()
    setSession(next)
    setFeedback(next.state === 'ready' ? 'Signed in. Your authorized scope is ready.' : 'Sign-in could not be completed.')
  }

  async function signOut() {
    await gateways.auth.signOut()
    setSession({ state: 'expired' })
    setFeedback('Signed out.')
  }

  const authenticated = session.state === 'ready'
  const scope = authenticated ? session.scopeLabel ?? 'Authorized scope' : session.state === 'loading' ? 'Loading scope' : 'No authorized scope'

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="#configuration" onClick={() => setRoute('configuration')}>
          <span className="brand-mark" aria-hidden="true">I</span>
          <span>IDEA Utility Monitoring</span>
        </a>
        <div className="session-controls">
          <span className="scope-pill" aria-label="Current scope">{scope}</span>
          {authenticated ? <button className="button button-quiet" type="button" onClick={() => void signOut()}>Sign out</button> : <button className="button button-primary" type="button" onClick={() => void signIn()} disabled={session.state === 'loading'}>Sign in</button>}
        </div>
      </header>
      <div className="layout">
        <nav className="sidebar" aria-label="Primary navigation">
          <p className="nav-heading">Workspace</p>
          {(['configuration', 'simulator', 'telemetry', 'audit'] as WebRoute[]).map((item) => <button className={`nav-link${route === item ? ' active' : ''}`} type="button" key={item} aria-current={route === item ? 'page' : undefined} onClick={() => setRoute(item)}>{item === 'configuration' ? 'Configuration' : item === 'simulator' ? 'Simulator' : item === 'telemetry' ? 'Latest & health' : 'Audit review'}</button>)}
          <div className="sidebar-note"><span className="status-dot" aria-hidden="true" /><span>Provider-neutral mode</span></div>
        </nav>
        <main className="content" id="main-content">
          <div className="feedback" role="status" aria-live="polite">{feedback}</div>
          {!authenticated && <section className="notice notice-info" aria-label="Authentication notice"><strong>{session.state === 'loading' ? 'Loading your session.' : 'Sign in to manage this workspace.'}</strong><span>Queries and mutations are scope-checked server-side.</span></section>}
          {(session.state === 'forbidden' || session.state === 'expired') && <section className="notice notice-warning" role="alert">Your session is {session.state}. Sign in again to continue.</section>}
          {children(route)}
        </main>
      </div>
    </div>
  )
}

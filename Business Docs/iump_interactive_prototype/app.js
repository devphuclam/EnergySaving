(() => {
  'use strict';

  const sourceData = window.IUMP_MOCK_DATA;
  const clone = (value) => JSON.parse(JSON.stringify(value));

  const state = {
    data: clone(sourceData),
    currentRoute: '#/overview',
    alertFilters: { severity: 'All', state: 'All', owner: 'All', search: '' },
    alertSort: 'severity',
    pointTab: 'Overview',
    pointRange: '24h',
    ruleStep: 1,
    ruleTested: false,
    ruleSubmitted: false,
    ruleForm: {
      name: 'Main Panel A — Power High Limit',
      purpose: 'Alert when active power remains above the approved operating limit.',
      site: 'IDEA Factory A',
      area: 'Utility Area',
      asset: 'Main Panel A',
      point: 'MAIN-PANEL-A-POWER',
      type: 'Threshold',
      operator: '>',
      threshold: '120',
      unit: 'kW',
      duration: '5',
      cooldown: '30',
      severity: 'Critical',
      schedule: 'All operating hours',
      rationale: 'Approved provisional limit for prototype review. Requires Technical/Energy Reviewer validation before production use.'
    },
    csvStep: 3,
    csvFilter: 'All',
    drawer: null,
    toastId: 0,
    loadingDemo: false
  };

  const $ = (selector, root = document) => root.querySelector(selector);
  const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
  const escapeHtml = (value) => String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');

  const slug = (value) => String(value || '').toLowerCase().replaceAll(' ', '-').replaceAll('/', '-');

  function icon(name, label = '') {
    const paths = {
      overview: '<rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/>',
      chart: '<path d="M3 3v18h18"/><path d="m7 16 4-5 4 3 5-7"/>',
      alert: '<path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/>',
      asset: '<path d="M3 21h18"/><path d="M5 21V7l7-4 7 4v14"/><path d="M9 21v-6h6v6"/>',
      rule: '<path d="M4 4h16v4H4z"/><path d="M4 12h10v4H4z"/><path d="M4 20h6"/><path d="m17 15 2 2 3-4"/>',
      source: '<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v6c0 1.7 3.6 3 8 3s8-1.3 8-3V5"/><path d="M4 11v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6"/>',
      import: '<path d="M12 3v12"/><path d="m7 10 5 5 5-5"/><path d="M5 21h14"/>',
      report: '<path d="M6 2h9l5 5v15H6z"/><path d="M14 2v6h6"/><path d="M9 13h8M9 17h8M9 9h2"/>',
      audit: '<path d="M9 3h6l1 2h3v16H5V5h3z"/><path d="m9 14 2 2 4-5"/>',
      users: '<circle cx="9" cy="8" r="4"/><path d="M2 21c0-4 3-7 7-7s7 3 7 7"/><path d="M17 11a3 3 0 1 0 0-6"/><path d="M17 14c3 0 5 2 5 5"/>',
      health: '<path d="M3 12h4l2-6 4 12 2-6h6"/>',
      search: '<circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/>',
      bell: '<path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/>',
      chevron: '<path d="m9 18 6-6-6-6"/>',
      down: '<path d="m6 9 6 6 6-6"/>',
      refresh: '<path d="M20 6v6h-6"/><path d="M4 18v-6h6"/><path d="M18.5 9A7 7 0 0 0 6 5.5L4 8"/><path d="M5.5 15A7 7 0 0 0 18 18.5L20 16"/>',
      clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
      check: '<path d="m5 12 4 4L19 6"/>',
      x: '<path d="M6 6l12 12M18 6 6 18"/>',
      info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7h.01"/>',
      warning: '<path d="M12 3 2 21h20z"/><path d="M12 9v5M12 18h.01"/>',
      plus: '<path d="M12 5v14M5 12h14"/>',
      filter: '<path d="M4 5h16M7 12h10M10 19h4"/>',
      more: '<circle cx="5" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/>',
      arrowLeft: '<path d="m15 18-6-6 6-6"/>',
      arrowRight: '<path d="m9 18 6-6-6-6"/>',
      user: '<circle cx="12" cy="8" r="4"/><path d="M4 21c0-5 3.5-8 8-8s8 3 8 8"/>',
      upload: '<path d="M12 21V9"/><path d="m7 14 5-5 5 5"/><path d="M5 3h14"/>',
      file: '<path d="M6 2h9l5 5v15H6z"/><path d="M14 2v6h6"/>',
      settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1-2.8 2.8-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.5V21h-4v-.1a1.7 1.7 0 0 0-1-1.5 1.7 1.7 0 0 0-1.9.3l-.1.1L4.2 17l.1-.1a1.7 1.7 0 0 0 .3-1.9 1.7 1.7 0 0 0-1.5-1H3v-4h.1a1.7 1.7 0 0 0 1.5-1 1.7 1.7 0 0 0-.3-1.9L4.2 7 7 4.2l.1.1a1.7 1.7 0 0 0 1.9.3 1.7 1.7 0 0 0 1-1.5V3h4v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.9-.3l.1-.1L19.8 7l-.1.1a1.7 1.7 0 0 0-.3 1.9 1.7 1.7 0 0 0 1.5 1h.1v4h-.1a1.7 1.7 0 0 0-1.5 1z"/>',
      external: '<path d="M14 3h7v7"/><path d="M10 14 21 3"/><path d="M21 14v7H3V3h7"/>',
      list: '<path d="M8 6h13M8 12h13M8 18h13"/><circle cx="3" cy="6" r="1"/><circle cx="3" cy="12" r="1"/><circle cx="3" cy="18" r="1"/>'
    };
    return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="${label ? 'false' : 'true'}" ${label ? `aria-label="${escapeHtml(label)}"` : ''}>${paths[name] || paths.info}</svg>`;
  }

  const navGroups = [
    { label: 'MONITORING', items: [
      { route: '#/overview', label: 'Overview', icon: 'overview' },
      { route: '#/telemetry', label: 'Telemetry', icon: 'chart' },
      { route: '#/alerts', label: 'Alerts', icon: 'alert', count: () => activeAlerts().length }
    ]},
    { label: 'CONFIGURATION', items: [
      { route: '#/assets', label: 'Sites & Assets', icon: 'asset' },
      { route: '#/points/point-power-a', label: 'Measurement Points', icon: 'chart' },
      { route: '#/sources', label: 'Data Sources', icon: 'source' },
      { route: '#/simulator', label: 'Simulator', icon: 'refresh' },
      { route: '#/imports/new', label: 'Imports', icon: 'import' },
      { route: '#/rules/new', label: 'Rules', icon: 'rule' }
    ]},
    { label: 'MANAGEMENT', items: [
      { route: '#/reports', label: 'Reports', icon: 'report' },
      { route: '#/audit', label: 'Audit Log', icon: 'audit' }
    ]},
    { label: 'SYSTEM', items: [
      { route: '#/admin/users', label: 'Users & Access', icon: 'users' },
      { route: '#/system-health', label: 'System Health', icon: 'health' }
    ]}
  ];

  function activeAlerts() {
    return state.data.alerts.filter(a => !['Closed', 'False Positive', 'Ignored'].includes(a.state));
  }

  function normalizeStateClass(value) {
    return slug(value).replace('false-positive', 'unknown');
  }

  function badge(label, kind = label) {
    return `<span class="badge ${normalizeStateClass(kind)}">${escapeHtml(label)}</span>`;
  }
  function severityBadge(value) { return badge(value, value); }
  function qualityBadge(value) { return badge(value, value); }
  function statusBadge(value) { return badge(value, value); }

  function navigate(route) {
    if (!route.startsWith('#')) route = `#${route}`;
    if (location.hash === route) {
      state.currentRoute = route;
      renderApp();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
      location.hash = route;
    }
  }

  function currentRouteInfo() {
    const hash = location.hash || '#/overview';
    const clean = hash.replace(/^#\//, '');
    const [section, id] = clean.split('/');
    return { hash, section: section || 'overview', id };
  }

  function routeIsActive(route) {
    const current = currentRouteInfo();
    if (route === '#/overview') return current.section === 'overview';
    if (route.startsWith('#/points')) return current.section === 'points';
    if (route.startsWith('#/rules')) return current.section === 'rules';
    if (route.startsWith('#/imports')) return current.section === 'imports';
    return current.hash === route || current.hash.startsWith(route + '/');
  }

  function appShell(pageHtml) {
    const app = state.data.app;
    const sidebar = navGroups.map(group => `
      <nav class="nav-group" aria-label="${escapeHtml(group.label)}">
        <div class="nav-label">${escapeHtml(group.label)}</div>
        ${group.items.map(item => `
          <a class="nav-link ${routeIsActive(item.route) ? 'active' : ''}" href="${item.route}" data-route="${item.route}">
            <span class="nav-icon">${icon(item.icon)}</span>
            <span class="nav-text">${escapeHtml(item.label)}</span>
            ${item.count ? `<span class="nav-count">${item.count()}</span>` : ''}
          </a>`).join('')}
      </nav>`).join('');

    return `
      <div class="app-shell">
        <aside class="sidebar" aria-label="Điều hướng chính">
          <div class="brand">
            <div class="brand-mark">IU</div>
            <div class="brand-copy"><strong>IUMP</strong><span>Utility Monitoring</span></div>
          </div>
          <div class="sidebar-scroll">${sidebar}</div>
          <div class="sidebar-footer">
            <div class="sidebar-health"><span class="health-dot"></span><span>All prototype services healthy</span></div>
            <div class="footer-copy">${escapeHtml(app.environment)} · ${escapeHtml(app.version)}</div>
          </div>
        </aside>
        <header class="topbar">
          <div class="scope-control">
            <select class="scope-select" id="site-selector" aria-label="Chọn Site">
              ${state.data.sites.map(site => `<option value="${site.id}" ${site.name === app.site ? 'selected' : ''}>${escapeHtml(site.name)}</option>`).join('')}
            </select>
            <select class="scope-select hide-mobile" id="area-selector" aria-label="Chọn Area">
              <option>${escapeHtml(app.area)}</option>
              <option>Utility Area</option>
              <option>Compressed Air</option>
              <option>Production Line 1</option>
            </select>
          </div>
          <div class="topbar-divider"></div>
          <div class="cutoff">
            <strong>Data cutoff · Live</strong>
            <span>${escapeHtml(app.lastRefresh)} · ${escapeHtml(app.timezone)}</span>
          </div>
          <div class="topbar-spacer"></div>
          <button class="topbar-action" type="button" data-action="open-notifications" aria-label="Mở thông báo">
            ${icon('bell')}<span class="notification-dot"></span>
          </button>
          <button class="user-chip" type="button" data-action="open-user" aria-label="Mở thông tin người dùng">
            <span class="avatar">${escapeHtml(app.user.initials)}</span>
            <span class="user-meta"><strong>${escapeHtml(app.user.name)}</strong><span>${escapeHtml(app.user.role)} · Site scope</span></span>
            ${icon('down')}
          </button>
        </header>
        <main id="main-content" class="main" tabindex="-1"><div class="content-wrap">${pageHtml}</div></main>
        <div class="prototype-ribbon">INTERACTIVE PROTOTYPE</div>
        ${mobileNav()}
      </div>
      ${state.drawer ? renderDrawer(state.drawer) : ''}
      <div class="toast-stack" id="toast-stack" aria-live="polite"></div>`;
  }

  function mobileNav() {
    const items = [
      { route: '#/overview', label: 'Overview', icon: 'overview' },
      { route: '#/telemetry', label: 'Telemetry', icon: 'chart' },
      { route: '#/alerts', label: 'Alerts', icon: 'alert' },
      { route: '#/points/point-power-a', label: 'Point', icon: 'asset' },
      { route: '#/rules/new', label: 'Rule', icon: 'rule' }
    ];
    return `<nav class="mobile-nav" aria-label="Điều hướng di động">${items.map(item => `<a class="mobile-link ${routeIsActive(item.route) ? 'active' : ''}" href="${item.route}" data-route="${item.route}">${icon(item.icon)}<span>${item.label}</span></a>`).join('')}</nav>`;
  }

  function pageHeader({ title, subtitle, breadcrumbs = [], actions = '' }) {
    return `<div class="page-header">
      <div>
        ${breadcrumbs.length ? `<div class="breadcrumbs">${breadcrumbs.map((b, i) => i === 0 ? `<a href="${b.route}" data-route="${b.route}">${escapeHtml(b.label)}</a>` : `<span>${escapeHtml(b.label)}</span>`).join('')}</div>` : ''}
        <h1 class="page-title">${escapeHtml(title)}</h1>
        ${subtitle ? `<p class="page-subtitle">${escapeHtml(subtitle)}</p>` : ''}
      </div>
      ${actions ? `<div class="page-actions">${actions}</div>` : ''}
    </div>`;
  }

  function kpiCard({ label, value, detail, foot, route, iconName, tone = '' }) {
    return `<a class="kpi-card" href="${route}" data-route="${route}">
      <div class="kpi-top"><span>${escapeHtml(label)}</span><span class="kpi-icon ${tone}">${icon(iconName)}</span></div>
      <div class="kpi-value">${escapeHtml(value)}</div>
      <div class="kpi-detail">${detail}</div>
      <div class="kpi-foot">${escapeHtml(foot)}</div>
    </a>`;
  }

  function buildPathSegments(values, width, height, minValue, maxValue, padding = 26) {
    const usableW = width - padding * 2;
    const usableH = height - padding * 2;
    const step = usableW / Math.max(1, values.length - 1);
    const y = (v) => padding + (maxValue - v) / Math.max(1, maxValue - minValue) * usableH;
    const segments = [];
    let current = [];
    values.forEach((v, i) => {
      if (v === null || Number.isNaN(v)) {
        if (current.length) segments.push(current);
        current = [];
      } else {
        current.push([padding + i * step, y(v)]);
      }
    });
    if (current.length) segments.push(current);
    return { segments, y, step, padding };
  }

  function timeSeriesSvg({ values, labels = [], threshold = 120, alertStart = null, alertEnd = null, title = 'Biểu đồ telemetry' }) {
    const width = 900;
    const height = 270;
    const numeric = values.filter(v => v !== null);
    const minValue = Math.floor(Math.min(...numeric, threshold) - 8);
    const maxValue = Math.ceil(Math.max(...numeric, threshold) + 8);
    const { segments, y, step, padding } = buildPathSegments(values, width, height, minValue, maxValue, 34);
    const pathStrings = segments.map(points => points.map((p, i) => `${i === 0 ? 'M' : 'L'}${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' '));
    const thresholdY = y(threshold);
    const gapIndexes = values.map((v, i) => v === null ? i : -1).filter(i => i >= 0);
    const gapStart = gapIndexes.length ? Math.min(...gapIndexes) : null;
    const gapEnd = gapIndexes.length ? Math.max(...gapIndexes) : null;
    const alertX = alertStart !== null ? padding + alertStart * step : null;
    const alertW = alertStart !== null ? Math.max(step, (alertEnd - alertStart + 1) * step) : 0;
    const xTicks = [0, Math.floor((values.length - 1) / 3), Math.floor((values.length - 1) * 2 / 3), values.length - 1];
    return `
      <svg class="chart-svg" viewBox="0 0 ${width} ${height}" role="img" aria-label="${escapeHtml(title)}">
        <title>${escapeHtml(title)}</title>
        <desc>Đường màu xanh là dữ liệu hợp lệ. Đường đỏ nét đứt là ngưỡng. Khoảng xám đứt nét là dữ liệu Missing, không phải giá trị 0.</desc>
        <rect x="0" y="0" width="${width}" height="${height}" rx="7" fill="#ffffff"/>
        ${[0, .25, .5, .75, 1].map(t => {
          const yy = 34 + t * (height - 68);
          const val = Math.round(maxValue - t * (maxValue - minValue));
          return `<line x1="34" y1="${yy}" x2="866" y2="${yy}" stroke="#e5ebf0" stroke-width="1"/><text x="5" y="${yy + 4}" font-size="10" fill="#7b8794">${val}</text>`;
        }).join('')}
        ${alertX !== null ? `<rect x="${alertX}" y="34" width="${alertW}" height="202" fill="rgba(198,40,40,.08)" stroke="rgba(198,40,40,.20)"/>` : ''}
        <line x1="34" y1="${thresholdY}" x2="866" y2="${thresholdY}" stroke="#c62828" stroke-width="1.5" stroke-dasharray="6 5"/>
        <text x="775" y="${thresholdY - 7}" font-size="10" fill="#c62828">Threshold ${threshold}</text>
        ${gapStart !== null ? `<rect x="${padding + gapStart * step - step * .35}" y="34" width="${Math.max(step, (gapEnd-gapStart+1)*step)}" height="202" fill="rgba(102,112,133,.05)" stroke="#667085" stroke-dasharray="4 4"/><text x="${padding + gapStart * step}" y="52" font-size="10" fill="#667085">Missing</text>` : ''}
        ${pathStrings.map(d => `<path d="${d}" fill="none" stroke="#2f75b5" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/>`).join('')}
        ${values.map((v, i) => v === null ? '' : `<circle cx="${padding + i * step}" cy="${y(v)}" r="${i === values.length - 1 ? 4 : 2.2}" fill="#ffffff" stroke="#2f75b5" stroke-width="${i === values.length - 1 ? 2.5 : 1.5}"/>`).join('')}
        ${xTicks.map(i => `<text x="${padding + i * step}" y="258" text-anchor="middle" font-size="10" fill="#7b8794">${escapeHtml(labels[i] || '')}</text>`).join('')}
      </svg>`;
  }

  function compactAlertTable(alerts) {
    return `<div class="table-wrap"><table class="data-table">
      <thead><tr><th>Severity</th><th>Alert</th><th>State</th><th>Owner</th><th class="right">Age</th></tr></thead>
      <tbody>${alerts.slice(0,5).map(a => `<tr class="clickable" data-route="#/alerts/${a.id}" tabindex="0">
        <td>${severityBadge(a.severity)}</td>
        <td><div class="cell-title">${escapeHtml(a.title)}</div><div class="cell-subtitle">${escapeHtml(a.asset)} · <span class="mono">${escapeHtml(a.point)}</span></div></td>
        <td>${statusBadge(a.state)}</td>
        <td>${escapeHtml(a.owner)}</td>
        <td class="right mono">${escapeHtml(a.age)}</td>
      </tr>`).join('')}</tbody></table></div>`;
  }

  function renderOverview() {
    const point = state.data.points['point-power-a'];
    const highCount = activeAlerts().filter(a => ['Critical', 'High'].includes(a.severity)).length;
    const missingCount = state.data.qualitySummary.find(q => q.label === 'Missing').count;
    const onlineCount = state.data.sources.filter(s => ['Online', 'Completed'].includes(s.state)).length;
    const staleCount = state.data.sources.filter(s => s.state === 'Stale').length;
    const header = pageHeader({
      title: 'Operations Overview',
      subtitle: 'Monitor utility data health, active alerts and assets requiring attention for the selected site.',
      actions: `<button class="button" type="button" data-action="refresh">${icon('refresh')}Refresh</button><a class="button primary" href="#/alerts" data-route="#/alerts">${icon('alert')}Open alert queue</a>`
    });

    const kpis = `<div class="kpi-grid">
      ${kpiCard({ label: 'Active Alerts', value: activeAlerts().length, detail: `${severityBadge('Critical')} ${highCount} High/Critical`, foot: 'Updated less than 1 minute ago', route: '#/alerts', iconName: 'alert' })}
      ${kpiCard({ label: 'High / Critical', value: highCount, detail: `<span class="text-danger">Needs operator attention</span>`, foot: '1 unassigned critical alert', route: '#/alerts', iconName: 'warning' })}
      ${kpiCard({ label: 'Missing Intervals', value: missingCount, detail: `${qualityBadge('Missing')} Not interpreted as 0`, foot: 'Across 4 measurement points', route: '#/telemetry', iconName: 'clock' })}
      ${kpiCard({ label: 'Source Health', value: `${onlineCount}/${state.data.sources.length}`, detail: `${statusBadge('Online')} ${staleCount} stale source`, foot: 'Last source check 15 seconds ago', route: '#/sources', iconName: 'health' })}
    </div>`;

    const qualityRows = state.data.qualitySummary.map(q => `<div class="quality-row"><div class="left"><span class="quality-dot" style="background:${q.label === 'Good' ? 'var(--success)' : q.label === 'Uncertain' ? '#d98500' : q.label === 'Bad' ? 'var(--danger)' : 'var(--unknown)'}"></span><span>${escapeHtml(q.label)}</span></div><strong>${q.value}% <span class="muted">· ${q.count}</span></strong></div>`).join('');

    const sourceRows = state.data.sources.slice(0,4).map(s => `<div class="status-row"><div><div class="status-row-title mono">${escapeHtml(s.name)}</div><div class="status-row-subtitle">${escapeHtml(s.type)} · ${escapeHtml(s.lastSeen)}</div></div>${statusBadge(s.state)}</div>`).join('');

    return `${header}${kpis}
      <div class="dashboard-grid">
        <section class="card">
          <div class="card-header"><div><h2>Total Power · Last 24 hours</h2><p>${escapeHtml(point.code)} · Expected interval 60 seconds · Coverage ${point.coverage}%</p></div><a class="card-link" href="#/points/${point.id}" data-route="#/points/${point.id}">Point detail</a></div>
          <div class="card-body"><div class="chart-wrap">${timeSeriesSvg({ values: point.series, labels: point.timestamps, threshold: 120, alertStart: 12, alertEnd: 16, title: 'Total Power for Main Panel A during the last 24 hours' })}</div>
          <div class="chart-legend"><span class="legend-item"><span class="legend-line"></span>Good telemetry</span><span class="legend-item"><span class="legend-line dashed"></span>Threshold</span><span class="legend-item"><span class="legend-box"></span>Alert window</span><span class="legend-item"><span class="legend-gap"></span>Missing gap</span></div>
          <div class="chart-note">${icon('info')}<span>Missing intervals create a visible gap. The prototype never converts Missing data into the numeric value 0.</span></div></div>
        </section>
        <section class="card">
          <div class="card-header"><div><h2>Data Quality</h2><p>Current selected scope</p></div><a class="card-link" href="#/telemetry" data-route="#/telemetry">Explore</a></div>
          <div class="card-body"><div class="quality-bar" aria-label="Data quality distribution"><span class="good" style="width:92%"></span><span class="uncertain" style="width:3%"></span><span class="bad" style="width:1%"></span><span class="missing" style="width:4%"></span></div><div class="quality-list">${qualityRows}</div></div>
        </section>
      </div>
      <div class="dashboard-grid">
        <section class="card">
          <div class="card-header"><div><h2>Alert Queue</h2><p>Sorted by operational priority and age</p></div><a class="card-link" href="#/alerts" data-route="#/alerts">View all alerts</a></div>
          <div class="card-body" style="padding:0">${compactAlertTable(activeAlerts())}</div>
        </section>
        <section class="card">
          <div class="card-header"><div><h2>Source Status</h2><p>Last received and ingestion health</p></div><a class="card-link" href="#/sources" data-route="#/sources">View diagnostics</a></div>
          <div class="card-body"><div class="status-list">${sourceRows}</div></div>
        </section>
      </div>
      <div class="dashboard-grid">
        <section class="card span-2">
          <div class="card-header"><div><h2>Assets Requiring Attention</h2><p>Assets are ranked by active alerts, data quality and source freshness.</p></div></div>
          <div class="card-body" style="padding:0"><div class="table-wrap"><table class="data-table"><thead><tr><th>Asset</th><th>Primary point</th><th>Issue</th><th>Owner</th><th>Last data</th></tr></thead><tbody>
            <tr class="clickable" data-route="#/points/point-power-a"><td><div class="cell-title">Main Panel A</div><div class="cell-subtitle">Utility Area</div></td><td class="mono">MAIN-PANEL-A-POWER</td><td>${severityBadge('Critical')} Power high</td><td>Unassigned</td><td>10:25 · ${qualityBadge('Good')}</td></tr>
            <tr class="clickable" data-route="#/points/point-compressor-a"><td><div class="cell-title">Compressor A</div><div class="cell-subtitle">Compressed Air</div></td><td class="mono">COMPRESSOR-A-POWER</td><td>${severityBadge('High')} Near limit</td><td>Nguyễn An</td><td>10:24 · ${qualityBadge('Good')}</td></tr>
            <tr><td><div class="cell-title">REST-04 Source</div><div class="cell-subtitle">Production Line 1</div></td><td class="mono">—</td><td>${statusBadge('Stale')} No data for 8 minutes</td><td>Integration Team</td><td>10:17 · ${qualityBadge('Missing')}</td></tr>
          </tbody></table></div></div>
        </section>
      </div>`;
  }

  function filteredAlerts() {
    let list = [...state.data.alerts];
    const f = state.alertFilters;
    if (f.severity !== 'All') list = list.filter(a => a.severity === f.severity);
    if (f.state !== 'All') list = list.filter(a => a.state === f.state);
    if (f.owner !== 'All') list = list.filter(a => f.owner === 'Unassigned' ? a.owner === 'Unassigned' : a.owner !== 'Unassigned');
    if (f.search.trim()) {
      const term = f.search.toLowerCase();
      list = list.filter(a => [a.id,a.title,a.asset,a.point,a.owner,a.ruleId].join(' ').toLowerCase().includes(term));
    }
    const sevRank = { Critical: 0, High: 1, Medium: 2, Low: 3, Info: 4 };
    if (state.alertSort === 'severity') list.sort((a,b) => sevRank[a.severity] - sevRank[b.severity]);
    if (state.alertSort === 'age') list.sort((a,b) => parseAgeMinutes(b.age) - parseAgeMinutes(a.age));
    if (state.alertSort === 'state') list.sort((a,b) => a.state.localeCompare(b.state));
    return list;
  }

  function parseAgeMinutes(age) {
    if (age.endsWith('m')) return Number(age.slice(0,-1));
    if (age.endsWith('h')) return Number(age.slice(0,-1)) * 60;
    if (age.endsWith('d')) return Number(age.slice(0,-1)) * 1440;
    return 0;
  }

  function renderAlertQueue() {
    const list = filteredAlerts();
    const header = pageHeader({
      title: 'Alert Queue',
      subtitle: 'A managed work queue for operational cases. Notifications do not replace ownership.',
      breadcrumbs: [{ label: 'Overview', route: '#/overview' }, { label: 'Alerts' }],
      actions: `<button class="button" type="button" data-action="export-alerts">${icon('report')}Export view</button><button class="button primary" type="button" data-action="assign-first">${icon('user')}Assign next critical</button>`
    });

    const filters = `<div class="filter-bar">
      <div class="field"><label for="filter-severity">Severity</label><select id="filter-severity" class="select" data-filter="severity">${['All','Critical','High','Medium','Low'].map(v => `<option ${state.alertFilters.severity === v ? 'selected' : ''}>${v}</option>`).join('')}</select></div>
      <div class="field"><label for="filter-state">State</label><select id="filter-state" class="select" data-filter="state">${['All','Open','Acknowledged','In Progress','Recovered','Resolved','Closed'].map(v => `<option ${state.alertFilters.state === v ? 'selected' : ''}>${v}</option>`).join('')}</select></div>
      <div class="field"><label for="filter-owner">Owner</label><select id="filter-owner" class="select" data-filter="owner">${['All','Unassigned','Assigned'].map(v => `<option ${state.alertFilters.owner === v ? 'selected' : ''}>${v}</option>`).join('')}</select></div>
      <div class="field"><label for="sort-alerts">Sort</label><select id="sort-alerts" class="select" data-action="sort-alerts"><option value="severity" ${state.alertSort === 'severity' ? 'selected' : ''}>Priority</option><option value="age" ${state.alertSort === 'age' ? 'selected' : ''}>Age</option><option value="state" ${state.alertSort === 'state' ? 'selected' : ''}>State</option></select></div>
      <div class="search-field"><label class="helper" for="alert-search">Search alerts</label>${icon('search')}<input id="alert-search" class="input" type="search" value="${escapeHtml(state.alertFilters.search)}" placeholder="Alert, asset, point, owner..." data-filter="search" /></div>
      <button class="button small" type="button" data-action="clear-alert-filters">Clear</button>
    </div>`;

    const rows = list.map(a => `<tr class="clickable" data-route="#/alerts/${a.id}" tabindex="0">
      <td data-label="Severity">${severityBadge(a.severity)}</td>
      <td class="alert-main"><div class="cell-title">${escapeHtml(a.title)}</div><div class="cell-subtitle"><span class="mono">${escapeHtml(a.id)}</span> · ${escapeHtml(a.ruleId)} v${a.ruleVersion}</div></td>
      <td data-label="Asset / Point"><div class="cell-title">${escapeHtml(a.asset)}</div><div class="cell-subtitle mono">${escapeHtml(a.point)}</div></td>
      <td data-label="State">${statusBadge(a.state)}</td>
      <td data-label="Owner">${a.owner === 'Unassigned' ? '<span class="text-danger">Unassigned</span>' : escapeHtml(a.owner)}</td>
      <td data-label="Age" class="right mono">${escapeHtml(a.age)}</td>
    </tr>`).join('');

    return `${header}${filters}
      <section class="card"><div class="card-header"><div><h2>${list.length} alerts</h2><p>Selected scope: ${escapeHtml(state.data.app.site)} · ${escapeHtml(state.data.app.area)}</p></div><div class="inline-meta"><span>${severityBadge('Critical')} ${list.filter(a=>a.severity==='Critical').length}</span><span>${statusBadge('Recovered')} ${list.filter(a=>a.state==='Recovered').length} pending review</span></div></div>
      <div class="card-body" style="padding:0">${list.length ? `<div class="table-wrap"><table class="data-table mobile-cards"><thead><tr><th>Severity</th><th>Alert</th><th>Asset / Point</th><th>State</th><th>Owner</th><th class="right">Age</th></tr></thead><tbody>${rows}</tbody></table></div>` : emptyState('No alerts match these filters','Try removing one or more filters.','clear-alert-filters')}</div></section>`;
  }

  function alertActionButtons(alert) {
    const buttons = [];
    if (alert.state === 'Open') buttons.push(['Acknowledge','acknowledge','primary'], ['Assign to me','assign','']);
    if (alert.state === 'Acknowledged') buttons.push(['Start work','start','primary'], ['Mark recovered','recover','']);
    if (alert.state === 'In Progress') buttons.push(['Mark recovered','recover',''], ['Resolve','resolve','primary']);
    if (alert.state === 'Recovered') buttons.push(['Resume investigation','start',''], ['Resolve after review','resolve','primary']);
    if (alert.state === 'Resolved') buttons.push(['Close alert','close','primary']);
    return buttons.map(([label,action,klass]) => `<button class="button ${klass}" type="button" data-alert-action="${action}" data-alert-id="${alert.id}">${label}</button>`).join('');
  }

  function renderAlertDetail(id) {
    const alert = state.data.alerts.find(a => a.id === id) || state.data.alerts[0];
    const point = state.data.points[alert.pointId] || state.data.points['point-power-a'];
    const rule = state.data.rules[alert.ruleId] || { id: alert.ruleId, version: alert.ruleVersion, name: 'Rule snapshot', type: 'Threshold', purpose: 'Rule snapshot stored with alert.' };
    const header = pageHeader({
      title: alert.title,
      subtitle: `${alert.id} · ${alert.site} / ${alert.area} / ${alert.asset}`,
      breadcrumbs: [{ label: 'Alerts', route: '#/alerts' }, { label: alert.id }],
      actions: `${alertActionButtons(alert)}<button class="button" type="button" data-action="open-alert-more">${icon('more')}More</button>`
    });

    const timeline = alert.timeline.map(t => `<div class="timeline-item"><div class="timeline-time">${escapeHtml(t.time)}</div><div class="timeline-content"><strong>${escapeHtml(t.title)}</strong><p>${escapeHtml(t.text)}</p></div></div>`).join('');

    const stateNotice = alert.state === 'Recovered'
      ? `<div class="notice success">${icon('check')}<div><strong>Condition recovered, case still open</strong><p>The source condition returned to normal at ${escapeHtml(alert.recoveryAt || 'the latest evaluation')}. An operator must review evidence and resolve or close the case.</p></div></div>`
      : alert.state === 'Closed' ? `<div class="notice info">${icon('info')}<div><strong>Alert closed</strong><p>This case is complete and retained for audit.</p></div></div>` : '';

    return `${header}${stateNotice}
      <div class="alert-title-row" style="margin-bottom:12px">${severityBadge(alert.severity)}${statusBadge(alert.state)}${qualityBadge(alert.quality)}<span class="mono muted">Rule ${escapeHtml(alert.ruleId)} v${alert.ruleVersion}</span></div>
      <div class="tabs" role="tablist"><button class="tab active" type="button">Overview</button><button class="tab" type="button">Chart</button><button class="tab" type="button">Timeline</button><button class="tab" type="button">Notes</button><button class="tab" type="button">Related</button></div>
      <div class="alert-summary-grid" style="margin-top:14px">
        <section class="card"><div class="card-header"><div><h2>Alert Summary</h2><p>Trigger and ownership context</p></div></div><div class="card-body"><div class="metric-stack">
          <div class="metric-box"><span>Trigger value</span><strong>${escapeHtml(alert.triggerValue)}</strong></div>
          <div class="metric-box"><span>Threshold / condition</span><strong>${escapeHtml(alert.threshold)}</strong></div>
          <div class="metric-box"><span>Violation duration</span><strong>${escapeHtml(alert.duration)}</strong></div>
          <div class="metric-box"><span>Owner</span><strong>${escapeHtml(alert.owner)}</strong></div>
          <div class="metric-box"><span>Data quality</span><strong>${qualityBadge(alert.quality)}</strong></div>
        </div></div></section>
        <section class="card"><div class="card-header"><div><h2>${escapeHtml(point.name)}</h2><p><span class="mono">${escapeHtml(point.code)}</span> · ${escapeHtml(point.source)} · ${escapeHtml(point.interval)}</p></div><a class="card-link" href="#/points/${point.id}" data-route="#/points/${point.id}">Open point detail</a></div><div class="card-body">
          <div class="chart-wrap compact">${timeSeriesSvg({ values: point.series, labels: point.timestamps, threshold: Number(rule.threshold || 120), alertStart: 12, alertEnd: 16, title: `Telemetry evidence for ${alert.id}` })}</div>
          <div class="chart-legend"><span class="legend-item"><span class="legend-line"></span>Telemetry</span><span class="legend-item"><span class="legend-line dashed"></span>Rule threshold</span><span class="legend-item"><span class="legend-box"></span>Trigger window</span></div>
        </div></section>
      </div>
      <div class="two-col" style="margin-top:14px">
        <section class="card"><div class="card-header"><div><h2>Rule Snapshot</h2><p>Immutable context stored with this alert</p></div><a class="card-link" href="#/rules/new" data-route="#/rules/new">Open rule workspace</a></div><div class="card-body">
          <div class="inline-meta" style="margin-bottom:12px"><span>${statusBadge(rule.state || 'Active')}</span><span class="mono">${escapeHtml(rule.id)} v${escapeHtml(rule.version || alert.ruleVersion)}</span></div>
          <p><strong>${escapeHtml(rule.name)}</strong></p><p class="muted">${escapeHtml(rule.purpose || '')}</p>
          <div class="metric-stack"><div class="metric-box"><span>Condition</span><strong>${escapeHtml(rule.type || 'Threshold')} ${escapeHtml(rule.operator || '')} ${escapeHtml(rule.threshold || '')} ${escapeHtml(rule.unit || '')}</strong></div><div class="metric-box"><span>Duration / cooldown</span><strong>${escapeHtml(rule.duration || alert.duration)} / ${escapeHtml(rule.cooldown || '30 minutes')}</strong></div></div>
        </div></section>
        <section class="card"><div class="card-header"><div><h2>Timeline</h2><p>State, ownership and notification events</p></div></div><div class="card-body"><div class="timeline">${timeline}</div></div></section>
      </div>`;
  }

  function updateAlertState(alertId, action) {
    const alert = state.data.alerts.find(a => a.id === alertId);
    if (!alert) return;
    const now = new Date();
    const time = now.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    const transitions = {
      acknowledge: { from: ['Open'], to: 'Acknowledged', title: 'Acknowledged by Trần Minh', text: 'Operator confirmed receipt of the alert.' },
      assign: { from: ['Open','Acknowledged','In Progress','Recovered'], to: alert.state, title: 'Assigned to Trần Minh', text: 'Ownership updated in the prototype.' },
      start: { from: ['Open','Acknowledged','Recovered'], to: 'In Progress', title: 'Investigation started', text: 'The alert is actively being investigated.' },
      recover: { from: ['Open','Acknowledged','In Progress'], to: 'Recovered', title: 'Condition marked recovered', text: 'The technical condition is normal, but the case remains open for review.' },
      resolve: { from: ['Acknowledged','In Progress','Recovered'], to: 'Resolved', title: 'Resolution submitted', text: 'Cause/action summary recorded in the prototype.' },
      close: { from: ['Resolved'], to: 'Closed', title: 'Alert closed', text: 'Manager/reviewer closure completed.' }
    };
    const transition = transitions[action];
    if (!transition || !transition.from.includes(alert.state)) {
      showToast('Action not allowed', `Cannot ${action} an alert in state ${alert.state}.`, 'warning');
      return;
    }
    if (action === 'assign') alert.owner = state.data.app.user.name;
    else {
      alert.state = transition.to;
      if (['acknowledge','start','resolve'].includes(action) && alert.owner === 'Unassigned') alert.owner = state.data.app.user.name;
      if (action === 'recover') alert.recoveryAt = time;
    }
    alert.timeline.push({ time, type: action === 'recover' ? 'System' : 'User', title: transition.title, text: transition.text });
    showToast('Alert updated', `${alert.id} is now ${alert.state}.`, alert.state === 'Recovered' ? 'success' : '');
    renderApp();
  }

  function renderPointDetail(id) {
    const point = state.data.points[id] || state.data.points['point-power-a'];
    const linkedAlerts = state.data.alerts.filter(a => a.pointId === point.id);
    const activeTab = state.pointTab;
    const tabs = ['Overview','History','Data Quality','Rules','Alerts','Source','Audit'];
    const header = pageHeader({
      title: point.code,
      subtitle: `${point.site} / ${point.area} / ${point.asset} · ${point.name}`,
      breadcrumbs: [{ label: 'Measurement Points', route: '#/points/point-power-a' }, { label: point.code }],
      actions: `<button class="button" type="button" data-action="point-more">${icon('settings')}Actions</button>`
    });
    const tabBar = `<div class="tabs" role="tablist">${tabs.map(t => `<button class="tab ${activeTab === t ? 'active' : ''}" type="button" data-point-tab="${t}">${escapeHtml(t)}</button>`).join('')}</div>`;
    const rangeButtons = ['Live','1h','24h','7d','30d','Custom'].map(r => `<button class="button small ${state.pointRange === r ? 'primary' : ''}" type="button" data-point-range="${r}">${r}</button>`).join('');
    let content = '';
    if (activeTab === 'Overview') {
      content = `<div class="dashboard-grid">
        <section class="card"><div class="card-header"><div><h2>${escapeHtml(point.metric)} Trend</h2><p>Expected interval ${escapeHtml(point.interval)} · Source ${escapeHtml(point.source)}</p></div><div class="page-actions">${rangeButtons}</div></div><div class="card-body">
          <div class="chart-wrap">${timeSeriesSvg({ values: point.series, labels: point.timestamps, threshold: 120, alertStart: 12, alertEnd: 16, title: `${point.name} telemetry` })}</div>
          <div class="chart-legend"><span class="legend-item"><span class="legend-line"></span>Valid telemetry</span><span class="legend-item"><span class="legend-gap"></span>Missing interval</span><span class="legend-item"><span class="legend-box"></span>Alert window</span></div>
        </div></section>
        <section class="card"><div class="card-header"><div><h2>Summary</h2><p>${state.pointRange} selected range</p></div></div><div class="card-body"><div class="metric-stack">
          <div class="metric-box"><span>Minimum</span><strong>${point.min} ${point.unit}</strong></div><div class="metric-box"><span>Maximum</span><strong>${point.max} ${point.unit}</strong></div><div class="metric-box"><span>Average</span><strong>${point.avg} ${point.unit}</strong></div><div class="metric-box"><span>Coverage</span><strong>${point.coverage}%</strong></div><div class="metric-box"><span>Valid / Missing</span><strong>${point.validRecords} / ${point.missingIntervals}</strong></div>
        </div></div></section></div>`;
    } else if (activeTab === 'History') {
      const rows = point.series.map((v,i) => `<tr><td>${escapeHtml(point.timestamps[i])}</td><td>${v === null ? '<span class="muted">—</span>' : `<strong>${v} ${point.unit}</strong>`}</td><td>${v === null ? qualityBadge('Missing') : qualityBadge('Good')}</td><td>${escapeHtml(point.source)}</td></tr>`).join('');
      content = `<section class="card"><div class="card-header"><div><h2>Measurement History</h2><p>Missing rows display no numeric value.</p></div></div><div class="card-body" style="padding:0"><div class="table-wrap"><table class="data-table"><thead><tr><th>Time</th><th>Value</th><th>Quality</th><th>Source</th></tr></thead><tbody>${rows}</tbody></table></div></div></section>`;
    } else if (activeTab === 'Data Quality') {
      content = `<div class="two-col"><section class="card"><div class="card-header"><div><h2>Quality Distribution</h2><p>Selected range</p></div></div><div class="card-body"><div class="quality-bar"><span class="good" style="width:92%"></span><span class="uncertain" style="width:3%"></span><span class="bad" style="width:1%"></span><span class="missing" style="width:4%"></span></div>${state.data.qualitySummary.map(q => `<div class="quality-row"><div class="left">${qualityBadge(q.label)}<span>${q.count} intervals</span></div><strong>${q.value}%</strong></div>`).join('')}</div></section><section class="card"><div class="card-header"><div><h2>Quality Rules</h2><p>Prototype interpretation</p></div></div><div class="card-body"><div class="notice info">${icon('info')}<div><strong>Missing is not a measurement</strong><p>No numeric zero is created. Charts retain a gap and No Data rules use expected interval plus grace period.</p></div></div><div class="metric-stack"><div class="metric-box"><span>Expected interval</span><strong>${point.interval}</strong></div><div class="metric-box"><span>Data cutoff</span><strong>${state.data.app.lastRefresh}</strong></div></div></div></section></div>`;
    } else if (activeTab === 'Rules') {
      content = `<section class="card"><div class="card-header"><div><h2>Rules using this point</h2><p>Rule versions remain linked to historical alerts.</p></div><a class="button primary small" href="#/rules/new" data-route="#/rules/new">Create rule</a></div><div class="card-body">${point.ruleIds.map(id => { const r=state.data.rules[id]; return `<div class="status-row"><div><div class="status-row-title">${escapeHtml(r.name)}</div><div class="status-row-subtitle mono">${r.id} v${r.version} · ${r.type} ${r.operator || ''} ${r.threshold || ''} ${r.unit || ''}</div></div>${statusBadge(r.state)}</div>`; }).join('')}</div></section>`;
    } else if (activeTab === 'Alerts') {
      content = `<section class="card"><div class="card-header"><div><h2>Alerts for this point</h2><p>${linkedAlerts.length} linked case(s)</p></div></div><div class="card-body" style="padding:0">${compactAlertTable(linkedAlerts)}</div></section>`;
    } else if (activeTab === 'Source') {
      const src = state.data.sources.find(s => s.name === point.source) || state.data.sources[0];
      content = `<section class="card"><div class="card-header"><div><h2>${escapeHtml(src.name)}</h2><p>${escapeHtml(src.type)}</p></div>${statusBadge(src.state)}</div><div class="card-body"><div class="three-col"><div class="metric-box"><span>Last seen</span><strong>${escapeHtml(src.lastSeen)}</strong></div><div class="metric-box"><span>Accepted records</span><strong>${escapeHtml(src.accepted)}</strong></div><div class="metric-box"><span>Expected interval</span><strong>${escapeHtml(point.interval)}</strong></div></div></div></section>`;
    } else {
      content = `<section class="card"><div class="card-header"><div><h2>Audit Events</h2><p>Prototype history for configuration and point access.</p></div></div><div class="card-body"><div class="timeline"><div class="timeline-item"><div class="timeline-time">09:40</div><div class="timeline-content"><strong>Point viewed</strong><p>${state.data.app.user.name} opened telemetry detail.</p></div></div><div class="timeline-item"><div class="timeline-time">08:15</div><div class="timeline-content"><strong>Source mapping verified</strong><p>Prototype configuration snapshot retained.</p></div></div></div></div></section>`;
    }

    return `${header}<div class="alert-title-row" style="margin-bottom:12px">${statusBadge('Active')}${statusBadge(point.source === 'REST-01' ? 'Online' : 'Online')}${qualityBadge(point.quality)}<span class="mono muted">Expected interval ${point.interval} · Source ${point.source}</span></div>
      <section class="card" style="margin-bottom:14px"><div class="card-body"><div class="page-header" style="margin:0"><div><div class="muted" style="font-size:12px">Latest value</div><div class="kpi-value">${point.value} <span style="font-size:16px">${point.unit}</span></div><div class="inline-meta" style="margin-top:8px"><span>${icon('clock')}${point.timestamp}</span><span>${qualityBadge(point.quality)}</span></div></div><div class="metric-stack" style="min-width:250px"><div class="metric-box"><span>Metric / Asset</span><strong>${point.metric} · ${point.asset}</strong></div></div></div></div>${tabBar}<div class="tab-panel">${content}</div></section>`;
  }

  function ruleProgress() {
    const steps = ['Basic info','Conditions','Settings','Schedule','Review'];
    return `<div class="progress-strip">${steps.map((s,i) => `<div class="progress-step ${state.ruleStep === i+1 ? 'active' : state.ruleStep > i+1 ? 'done' : ''}"><strong>${i+1}. ${escapeHtml(s)}</strong><span>${state.ruleStep > i+1 ? 'Completed' : state.ruleStep === i+1 ? 'Current step' : 'Pending'}</span></div>`).join('')}</div>`;
  }

  function ruleStepContent() {
    const f = state.ruleForm;
    if (state.ruleStep === 1) return `<div class="form-grid">
      <div class="field full"><label class="required" for="rule-name">Rule name</label><input id="rule-name" class="input" data-rule-field="name" value="${escapeHtml(f.name)}"></div>
      <div class="field full"><label class="required" for="rule-purpose">Purpose</label><textarea id="rule-purpose" class="textarea" data-rule-field="purpose">${escapeHtml(f.purpose)}</textarea></div>
      <div class="field"><label>Site</label><select class="select" data-rule-field="site"><option>${escapeHtml(f.site)}</option></select></div>
      <div class="field"><label>Area</label><select class="select" data-rule-field="area"><option>${escapeHtml(f.area)}</option><option>Compressed Air</option></select></div>
      <div class="field"><label>Asset</label><select class="select" data-rule-field="asset"><option>${escapeHtml(f.asset)}</option><option>Compressor A</option></select></div>
      <div class="field"><label>Measurement point</label><select class="select" data-rule-field="point"><option>${escapeHtml(f.point)}</option><option>COMPRESSOR-A-POWER</option></select></div>
    </div>`;
    if (state.ruleStep === 2) return `<div class="form-grid">
      <div class="field"><label>Rule type</label><select class="select" data-rule-field="type"><option>Threshold</option><option>No Data</option></select></div>
      <div class="field"><label>Operator</label><select class="select" data-rule-field="operator"><option>&gt;</option><option>&gt;=</option><option>&lt;</option><option>&lt;=</option></select></div>
      <div class="field"><label class="required">Threshold</label><input class="input" type="number" data-rule-field="threshold" value="${escapeHtml(f.threshold)}"></div>
      <div class="field"><label>Unit</label><input class="input" value="${escapeHtml(f.unit)}" readonly></div>
      <div class="field full"><div class="notice info">${icon('info')}<div><strong>Condition preview</strong><p>Trigger when ${escapeHtml(f.point)} remains ${escapeHtml(f.operator)} ${escapeHtml(f.threshold)} ${escapeHtml(f.unit)} for the configured duration.</p></div></div></div>
    </div>`;
    if (state.ruleStep === 3) return `<div class="form-grid">
      <div class="field"><label class="required">Duration (minutes)</label><input class="input" type="number" min="1" data-rule-field="duration" value="${escapeHtml(f.duration)}"></div>
      <div class="field"><label>Cooldown (minutes)</label><input class="input" type="number" min="0" data-rule-field="cooldown" value="${escapeHtml(f.cooldown)}"></div>
      <div class="field"><label class="required">Severity</label><select class="select" data-rule-field="severity">${['Critical','High','Medium','Low'].map(v => `<option ${f.severity===v?'selected':''}>${v}</option>`).join('')}</select></div>
      <div class="field"><label>Owner</label><input class="input" value="Lê Anh" readonly></div>
      <div class="field full"><label>Threshold rationale</label><textarea class="textarea" data-rule-field="rationale">${escapeHtml(f.rationale)}</textarea><span class="helper">High/Critical rules require rationale and test evidence before approval.</span></div>
    </div>`;
    if (state.ruleStep === 4) return `<div class="form-grid">
      <div class="field full"><label>Operating schedule</label><select class="select" data-rule-field="schedule"><option>${escapeHtml(f.schedule)}</option><option>Weekdays 07:00–18:00</option><option>Outside operating hours</option></select></div>
      <div class="field"><label>Effective date</label><input class="input" type="date" value="2026-07-24"></div>
      <div class="field"><label>Maintenance behavior</label><select class="select"><option>Annotate and suppress notification</option><option>Evaluate normally</option></select></div>
      <div class="field full"><div class="notice warning">${icon('warning')}<div><strong>Prototype schedule only</strong><p>Operating-calendar and maintenance approval workflows remain subject to Product Owner and Operations validation.</p></div></div></div>
    </div>`;
    const testPanel = state.ruleTested ? `<div class="result-panel"><div class="result-summary"><div><span>Violations</span><strong>2</strong></div><div><span>Recoveries</span><strong>2</strong></div><div><span>First trigger</span><strong>09:05</strong></div><div><span>Quality excluded</span><strong>4</strong></div></div><div class="card-body"><div class="notice success">${icon('check')}<div><strong>Test passed</strong><p>Actual violations and recoveries match the expected prototype outcome. Evidence ID: <span class="mono">TEST-PWR-001-0007</span>.</p></div></div><div class="chart-wrap compact">${timeSeriesSvg({ values: state.data.points['point-power-a'].series, labels: state.data.points['point-power-a'].timestamps, threshold: Number(f.threshold), alertStart: 12, alertEnd: 16, title: 'Rule test evidence chart' })}</div></div></div>` : `<div class="empty-state" style="min-height:250px"><div><div class="state-icon">${icon('rule')}</div><h3 class="state-title">Run a test before review</h3><p class="state-text">The prototype will evaluate the rule against sample telemetry, including a Missing gap and quality exclusions.</p><button class="button primary" type="button" data-action="run-rule-test">Run Test</button></div></div>`;
    return `<div class="two-col"><section class="card"><div class="card-header"><div><h2>Rule Review</h2><p>Summary before submission</p></div>${state.ruleSubmitted ? statusBadge('Submitted') : statusBadge('Draft')}</div><div class="card-body"><div class="metric-stack"><div class="metric-box"><span>Name</span><strong>${escapeHtml(f.name)}</strong></div><div class="metric-box"><span>Condition</span><strong>${escapeHtml(f.point)} ${escapeHtml(f.operator)} ${escapeHtml(f.threshold)} ${escapeHtml(f.unit)} for ${escapeHtml(f.duration)} min</strong></div><div class="metric-box"><span>Severity / cooldown</span><strong>${escapeHtml(f.severity)} / ${escapeHtml(f.cooldown)} min</strong></div><div class="metric-box"><span>Schedule</span><strong>${escapeHtml(f.schedule)}</strong></div></div></div></section><section>${testPanel}</section></div>`;
  }

  function renderRuleBuilder() {
    const header = pageHeader({
      title: 'Create Rule',
      subtitle: 'Configure, test and submit a versioned rule. Submission does not activate the rule.',
      breadcrumbs: [{ label: 'Rules', route: '#/rules' }, { label: 'New Rule' }],
      actions: `<button class="button" type="button" data-action="save-rule-draft">Save Draft</button>${state.ruleStep === 5 ? `<button class="button primary" type="button" data-action="submit-rule" ${!state.ruleTested || state.ruleSubmitted ? 'disabled' : ''}>${state.ruleSubmitted ? 'Submitted' : 'Submit for Review'}</button>` : ''}`
    });
    return `${header}${ruleProgress()}<section class="card"><div class="card-header"><div><h2>Step ${state.ruleStep} of 5</h2><p>${['Define identity and scope','Define the evaluation condition','Configure duration, cooldown and severity','Set operating schedule and effective behavior','Review test evidence and submit'][state.ruleStep-1]}</p></div>${state.ruleSubmitted ? statusBadge('Submitted') : statusBadge(state.ruleTested ? 'Tested' : 'Draft')}</div><div class="form-card">${ruleStepContent()}<div class="form-actions"><button class="button" type="button" data-action="rule-prev" ${state.ruleStep === 1 ? 'disabled' : ''}>${icon('arrowLeft')}Back</button><div class="page-actions">${state.ruleStep < 5 ? `<button class="button primary" type="button" data-action="rule-next">Next${icon('arrowRight')}</button>` : `<button class="button" type="button" data-action="run-rule-test">${icon('refresh')}Run Test</button><button class="button primary" type="button" data-action="submit-rule" ${!state.ruleTested || state.ruleSubmitted ? 'disabled' : ''}>${state.ruleSubmitted ? 'Submitted' : 'Submit for Review'}</button>`}</div></div></div></section>`;
  }

  function renderCsvImport() {
    const csv = state.data.csvImport;
    const steps = ['Upload','Mapping','Preview','Confirm','Processing','Result'];
    const filteredRows = state.csvFilter === 'All' ? csv.rows : csv.rows.filter(r => r.status === state.csvFilter);
    const header = pageHeader({
      title: 'Import CSV',
      subtitle: `${csv.filename} · Preview is read-only until explicit confirmation.`,
      breadcrumbs: [{ label: 'Imports', route: '#/imports' }, { label: csv.filename }],
      actions: state.csvStep === 3 ? `<button class="button" type="button" data-action="cancel-import">Cancel</button><button class="button primary" type="button" data-action="confirm-csv">${icon('check')}Confirm & Import Valid</button>` : ''
    });
    const wizard = `<div class="wizard-steps">${steps.map((s,i) => `<div class="wizard-step ${state.csvStep === i+1 ? 'active' : state.csvStep > i+1 ? 'done' : ''}"><span>${state.csvStep > i+1 ? '✓' : i+1}</span>${s}</div>`).join('')}</div>`;
    if (state.csvStep === 5) {
      return `${header}${wizard}<section class="card"><div class="card-body"><div class="empty-state" style="min-height:360px"><div><div class="state-icon">${icon('refresh')}</div><h2 class="state-title">Processing import batch</h2><p class="state-text">Valid and warning rows are moving through canonical validation. Invalid and duplicate rows remain excluded.</p><div class="notice info" style="text-align:left">${icon('info')}<div><strong>Batch ID</strong><p class="mono">IMP-2026-0008 · 11,940 candidate rows</p></div></div></div></div></div></section>`;
    }
    if (state.csvStep === 6) {
      return `${header}${wizard}<section class="card"><div class="card-body"><div class="empty-state" style="min-height:360px"><div><div class="state-icon">${icon('check')}</div><h2 class="state-title">Import completed with warnings</h2><p class="state-text">11,940 rows were accepted for processing. Invalid and duplicate rows were excluded and remain available in the error detail.</p><div class="summary-cards" style="margin-top:18px"><div class="summary-card"><span>Accepted</span><strong class="text-success">11,940</strong></div><div class="summary-card"><span>Rejected</span><strong class="text-danger">40</strong></div><div class="summary-card"><span>Duplicates</span><strong>20</strong></div><div class="summary-card"><span>Batch</span><strong class="mono" style="font-size:13px">IMP-2026-0008</strong></div></div><div class="page-actions" style="justify-content:center;margin-top:16px"><button class="button" type="button" data-action="download-errors">Download error detail</button><button class="button primary" type="button" data-action="reset-csv">View batch result</button></div></div></div></div></section>`;
    }
    const rowHtml = filteredRows.map(r => `<tr><td>${r.row}</td><td class="mono">${escapeHtml(r.point)}</td><td class="mono">${escapeHtml(r.timestamp)}</td><td>${escapeHtml(r.value)}</td><td>${escapeHtml(r.unit)}</td><td>${statusBadge(r.status)}</td><td><button class="button ghost small" type="button" data-row-detail="${r.row}">${r.reason === '—' ? 'No issues' : 'View reason'}</button></td></tr>`).join('');
    return `${header}${wizard}
      <div class="summary-cards"><div class="summary-card"><span>Total rows</span><strong>${csv.total.toLocaleString()}</strong></div><div class="summary-card"><span>Valid</span><strong class="text-success">${csv.valid.toLocaleString()}</strong></div><div class="summary-card"><span>Warnings</span><strong class="text-warning">${csv.warnings}</strong></div><div class="summary-card"><span>Invalid</span><strong class="text-danger">${csv.invalid}</strong></div><div class="summary-card"><span>Duplicates</span><strong>${csv.duplicates}</strong></div></div>
      <div class="notice info">${icon('info')}<div><strong>Preview only — no telemetry has been written</strong><p>Confirming will import valid and warning rows. Invalid and duplicate rows stay excluded with row-level reason codes.</p></div></div>
      <div class="filter-bar"><div class="field"><label for="csv-status">Row status</label><select id="csv-status" class="select" data-action="csv-filter">${['All','Valid','Warning','Invalid','Duplicate'].map(v => `<option ${state.csvFilter===v?'selected':''}>${v}</option>`).join('')}</select></div><div class="search-field"><label class="helper">Current file</label>${icon('file')}<input class="input" value="${escapeHtml(csv.filename)}" readonly></div><button class="button small" type="button" data-action="download-errors">Download error detail</button></div>
      <section class="card"><div class="card-header"><div><h2>Row Preview</h2><p>${filteredRows.length} sample row(s) shown · ${csv.total.toLocaleString()} total</p></div><span class="mono muted">UTF-8 · Asia/Ho_Chi_Minh</span></div><div class="card-body" style="padding:0"><div class="table-wrap"><table class="data-table"><thead><tr><th>Row</th><th>Point</th><th>Timestamp</th><th>Value</th><th>Unit</th><th>Status</th><th>Reason</th></tr></thead><tbody>${rowHtml}</tbody></table></div></div></section>`;
  }

  function placeholderPage(section) {
    const labels = {
      telemetry: 'Telemetry Explorer', assets: 'Sites & Assets', sources: 'Data Sources', simulator: 'Simulator', reports: 'Reports', audit: 'Audit Log', admin: 'Users & Access', 'system-health': 'System Health', rules: 'Rule List', imports: 'Import History'
    };
    const title = labels[section] || 'Prototype Screen';
    return `${pageHeader({ title, subtitle: 'This navigation destination is included for information architecture continuity but is not a P0 detailed screen in this prototype.', breadcrumbs: [{ label: 'Overview', route: '#/overview' }, { label: title }] })}
      <div class="empty-state"><div><div class="state-icon">${icon(section === 'sources' ? 'source' : section === 'reports' ? 'report' : section === 'audit' ? 'audit' : 'overview')}</div><h2 class="state-title">${escapeHtml(title)} is outside the detailed P0 prototype</h2><p class="state-text">Use the sidebar to open Operations Overview, Alert Queue, Alert Detail, Point Detail, Rule Builder or CSV Import Preview.</p><a class="button primary" href="#/overview" data-route="#/overview">Return to Overview</a></div></div>`;
  }

  function emptyState(title, text, action = '') {
    return `<div class="empty-state"><div><div class="state-icon">${icon('search')}</div><h3 class="state-title">${escapeHtml(title)}</h3><p class="state-text">${escapeHtml(text)}</p>${action ? `<button class="button" type="button" data-action="${action}">Reset</button>` : ''}</div></div>`;
  }

  function renderSpecialState(type) {
    const content = {
      empty: ['No alerts in this scope','This is a reusable empty state for lists and dashboards.','search'],
      loading: ['Loading operational data','The prototype simulates loading without hiding the current scope.','refresh'],
      permission: ['Permission required','Your current role can view this page but cannot perform the requested configuration action.','users'],
      error: ['Data could not be loaded','The request failed. A correlation ID is shown so Support can trace it.','warning']
    }[type] || ['State example','Prototype screen state.','info'];
    return `${pageHeader({ title: `${content[0]} — State Example`, subtitle: 'Reusable P0 component behavior.' })}<div class="${type === 'permission' ? 'permission-state' : type === 'error' ? 'error-state' : 'empty-state'}"><div><div class="state-icon">${icon(content[2])}</div><h2 class="state-title">${content[0]}</h2><p class="state-text">${content[1]}</p>${type === 'error' ? '<p class="mono muted">Correlation ID: CORR-2026-07-23-0018</p>' : ''}<a class="button primary" href="#/overview" data-route="#/overview">Return to Overview</a></div></div>`;
  }

  function renderDrawer(type) {
    if (type === 'notifications') {
      return `<div class="drawer-backdrop" data-action="close-drawer"><aside class="drawer" role="dialog" aria-modal="true" aria-label="Notification Center" data-drawer-panel><div class="drawer-header"><h2 class="drawer-title">Notification Center</h2><button class="icon-button" data-action="close-drawer" aria-label="Đóng">${icon('x')}</button></div><div class="drawer-body"><div class="notice danger">${icon('alert')}<div><strong>Critical alert opened</strong><p>ALT-2026-0031 · Main Panel A power exceeds approved limit.</p></div></div><div class="notice info">${icon('rule')}<div><strong>Rule review available</strong><p>Power High Limit v4 is waiting for reviewer action.</p></div></div><div class="notice success">${icon('check')}<div><strong>CSV batch completed</strong><p>IMP-2026-0007 completed with warnings.</p></div></div></div></aside></div>`;
    }
    if (type === 'user') {
      return `<div class="drawer-backdrop" data-action="close-drawer"><aside class="drawer" role="dialog" aria-modal="true" aria-label="User and prototype menu" data-drawer-panel><div class="drawer-header"><h2 class="drawer-title">${escapeHtml(state.data.app.user.name)}</h2><button class="icon-button" data-action="close-drawer" aria-label="Đóng">${icon('x')}</button></div><div class="drawer-body"><div class="metric-stack"><div class="metric-box"><span>Role</span><strong>${escapeHtml(state.data.app.user.role)}</strong></div><div class="metric-box"><span>Scope</span><strong>${escapeHtml(state.data.app.site)} / ${escapeHtml(state.data.app.area)}</strong></div></div><h3>Screen state examples</h3><div class="status-list"><a class="button" href="#/states/empty" data-route="#/states/empty">Empty state</a><a class="button" href="#/states/loading" data-route="#/states/loading">Loading state</a><a class="button" href="#/states/permission" data-route="#/states/permission">Permission denied</a><a class="button" href="#/states/error" data-route="#/states/error">Error state</a></div></div></aside></div>`;
    }
    return '';
  }

  function renderPage() {
    const route = currentRouteInfo();
    if (route.section === 'overview') return renderOverview();
    if (route.section === 'alerts' && route.id) return renderAlertDetail(route.id);
    if (route.section === 'alerts') return renderAlertQueue();
    if (route.section === 'points') return renderPointDetail(route.id);
    if (route.section === 'rules' && route.id === 'new') return renderRuleBuilder();
    if (route.section === 'imports' && route.id === 'new') return renderCsvImport();
    if (route.section === 'states') return renderSpecialState(route.id);
    return placeholderPage(route.section);
  }

  function renderApp() {
    state.currentRoute = location.hash || '#/overview';
    const app = document.getElementById('app');
    if (!app) return;
    app.innerHTML = appShell(renderPage());
    document.title = `${document.querySelector('.page-title')?.textContent || 'IUMP'} — Industrial Light Prototype`;
  }

  function showToast(title, message, tone = '') {
    state.toastId += 1;
    const id = `toast-${state.toastId}`;
    const stack = document.getElementById('toast-stack');
    if (!stack) return;
    stack.insertAdjacentHTML('beforeend', `<div id="${id}" class="toast ${tone}"><strong>${escapeHtml(title)}</strong><span>${escapeHtml(message)}</span></div>`);
    setTimeout(() => document.getElementById(id)?.remove(), 3200);
  }

  function openRowDetail(rowNum) {
    const row = state.data.csvImport.rows.find(r => r.row === Number(rowNum));
    if (!row) return;
    state.drawer = 'row-detail';
    const reasonHtml = `<div class="drawer-backdrop" data-action="close-drawer"><aside class="drawer" role="dialog" aria-modal="true" aria-label="CSV row detail" data-drawer-panel><div class="drawer-header"><h2 class="drawer-title">CSV Row ${row.row}</h2><button class="icon-button" data-action="close-drawer" aria-label="Đóng">${icon('x')}</button></div><div class="drawer-body"><div class="metric-stack"><div class="metric-box"><span>Point</span><strong class="mono">${escapeHtml(row.point)}</strong></div><div class="metric-box"><span>Timestamp</span><strong class="mono">${escapeHtml(row.timestamp)}</strong></div><div class="metric-box"><span>Value / Unit</span><strong>${escapeHtml(row.value)} ${escapeHtml(row.unit)}</strong></div><div class="metric-box"><span>Status</span><strong>${statusBadge(row.status)}</strong></div></div><div class="notice ${row.status === 'Invalid' ? 'danger' : row.status === 'Warning' ? 'warning' : 'info'}" style="margin-top:12px">${icon(row.status === 'Invalid' ? 'warning' : 'info')}<div><strong>Validation reason</strong><p>${escapeHtml(row.reason)}</p></div></div></div></aside></div>`;
    document.body.insertAdjacentHTML('beforeend', reasonHtml);
  }

  function handleClick(event) {
    const routeTarget = event.target.closest('[data-route]');
    if (routeTarget) {
      event.preventDefault();
      state.drawer = null;
      navigate(routeTarget.dataset.route);
      return;
    }
    const alertAction = event.target.closest('[data-alert-action]');
    if (alertAction) {
      updateAlertState(alertAction.dataset.alertId, alertAction.dataset.alertAction);
      return;
    }
    const pointTab = event.target.closest('[data-point-tab]');
    if (pointTab) { state.pointTab = pointTab.dataset.pointTab; renderApp(); return; }
    const pointRange = event.target.closest('[data-point-range]');
    if (pointRange) { state.pointRange = pointRange.dataset.pointRange; renderApp(); return; }
    const rowDetail = event.target.closest('[data-row-detail]');
    if (rowDetail) { openRowDetail(rowDetail.dataset.rowDetail); return; }

    const action = event.target.closest('[data-action]')?.dataset.action;
    if (!action) return;
    if (action === 'open-notifications') { state.drawer = 'notifications'; renderApp(); }
    if (action === 'open-user') { state.drawer = 'user'; renderApp(); }
    if (action === 'close-drawer') {
      if (event.target.closest('[data-drawer-panel]') && event.target.dataset.action !== 'close-drawer') return;
      state.drawer = null;
      document.querySelectorAll('.drawer-backdrop').forEach(el => el.remove());
      renderApp();
    }
    if (action === 'refresh') { state.data.app.lastRefresh = '23/07/2026 10:26:03 ICT'; showToast('Data refreshed','Latest prototype values and alert counts were refreshed.','success'); renderApp(); }
    if (action === 'clear-alert-filters') { state.alertFilters = { severity:'All', state:'All', owner:'All', search:'' }; renderApp(); }
    if (action === 'assign-first') {
      const critical = state.data.alerts.find(a => a.severity === 'Critical' && a.owner === 'Unassigned');
      if (critical) { critical.owner = state.data.app.user.name; critical.timeline.push({ time:'10:26', type:'User', title:`Assigned to ${state.data.app.user.name}`, text:'Assigned from the alert queue.' }); showToast('Critical alert assigned',`${critical.id} is now owned by ${state.data.app.user.name}.`,'success'); renderApp(); }
      else showToast('No unassigned critical alerts','The current filtered scope has no unassigned critical alert.','');
    }
    if (action === 'export-alerts') showToast('Export requested','Prototype export job queued. No real file is generated.','');
    if (action === 'rule-next') { state.ruleStep = Math.min(5, state.ruleStep + 1); renderApp(); }
    if (action === 'rule-prev') { state.ruleStep = Math.max(1, state.ruleStep - 1); renderApp(); }
    if (action === 'run-rule-test') { state.ruleTested = true; state.ruleStep = 5; showToast('Rule test passed','2 violations and 2 recoveries matched expected output.','success'); renderApp(); }
    if (action === 'submit-rule') {
      if (!state.ruleTested) { showToast('Test required','Run a successful test before submitting the rule.','warning'); return; }
      state.ruleSubmitted = true; showToast('Submitted for review','The rule remains inactive until a reviewer approves it.','success'); renderApp();
    }
    if (action === 'save-rule-draft') showToast('Draft saved','Prototype rule values were saved in local memory.','success');
    if (action === 'confirm-csv') {
      state.csvStep = 5; renderApp();
      setTimeout(() => { state.csvStep = 6; renderApp(); showToast('Import completed','11,940 rows accepted; 40 invalid and 20 duplicate rows excluded.','success'); }, 850);
    }
    if (action === 'cancel-import') { showToast('Import cancelled','No telemetry was written from the preview.',''); navigate('#/overview'); }
    if (action === 'reset-csv') { state.csvStep = 3; state.csvFilter = 'All'; renderApp(); }
    if (action === 'download-errors') showToast('Error detail prepared','Prototype does not create a real download.','');
    if (action === 'open-alert-more') showToast('More actions','Audit export and false-positive actions are represented in the specification but not expanded here.','');
    if (action === 'point-more') showToast('Point actions','Configuration actions are outside this P0 interaction flow.','');
  }

  function handleChange(event) {
    const filter = event.target.dataset.filter;
    if (filter) {
      state.alertFilters[filter] = event.target.value;
      renderApp();
      return;
    }
    const action = event.target.dataset.action;
    if (action === 'sort-alerts') { state.alertSort = event.target.value; renderApp(); return; }
    if (action === 'csv-filter') { state.csvFilter = event.target.value; renderApp(); return; }
    const ruleField = event.target.dataset.ruleField;
    if (ruleField) { state.ruleForm[ruleField] = event.target.value; state.ruleTested = false; state.ruleSubmitted = false; renderApp(); return; }
    if (event.target.id === 'site-selector') {
      const site = state.data.sites.find(s => s.id === event.target.value);
      if (site) { state.data.app.site = site.name; state.data.app.timezone = site.timezone; showToast('Scope changed',`Now viewing ${site.name}.`,''); renderApp(); }
    }
    if (event.target.id === 'area-selector') { state.data.app.area = event.target.value; showToast('Area changed',`Scope changed to ${event.target.value}.`,''); renderApp(); }
  }

  function handleInput(event) {
    if (event.target.dataset.filter === 'search') {
      state.alertFilters.search = event.target.value;
      const caret = event.target.selectionStart;
      renderApp();
      const input = document.getElementById('alert-search');
      if (input) { input.focus(); input.setSelectionRange(caret, caret); }
    }
  }

  function handleKeydown(event) {
    const row = event.target.closest('tr[data-route]');
    if (row && (event.key === 'Enter' || event.key === ' ')) {
      event.preventDefault();
      navigate(row.dataset.route);
    }
    if (event.key === 'Escape' && (state.drawer || document.querySelector('.drawer-backdrop'))) {
      state.drawer = null;
      document.querySelectorAll('.drawer-backdrop').forEach(el => el.remove());
      renderApp();
    }
  }

  window.addEventListener('hashchange', () => { state.drawer = null; renderApp(); window.scrollTo(0,0); });
  document.addEventListener('click', handleClick);
  document.addEventListener('change', handleChange);
  document.addEventListener('input', handleInput);
  document.addEventListener('keydown', handleKeydown);

  window.IumpPrototype = {
    state,
    navigate,
    renderApp,
    activeAlerts,
    updateAlertState,
    getMissingSamples: () => Object.values(state.data.points).flatMap(p => p.series).filter(v => v === null),
    isRecoveredClosed: (alertId) => {
      const a = state.data.alerts.find(x => x.id === alertId);
      return Boolean(a && a.state === 'Recovered' && a.state === 'Closed');
    }
  };

  if (!location.hash) history.replaceState(null, '', '#/overview');
  renderApp();
})();

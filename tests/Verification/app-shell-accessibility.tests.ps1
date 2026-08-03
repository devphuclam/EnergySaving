[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$appShellPath = Join-Path $repoRoot 'src\Web\src\app\AppShell.tsx'
$appCssPath = Join-Path $repoRoot 'src\Web\src\App.css'

if (-not (Test-Path -LiteralPath $appShellPath -PathType Leaf)) {
    throw "AppShell accessibility seam is missing: $appShellPath"
}

$source = Get-Content -LiteralPath $appShellPath -Raw
$styles = Get-Content -LiteralPath $appCssPath -Raw
$requiredTokens = @(
    'id="sign-in-username"',
    'htmlFor="sign-in-username"',
    "Tên đăng nhập",
    'className="sign-in-label"',
    'id="sign-in-password"',
    'htmlFor="sign-in-password"',
    "Mật khẩu",
    'id="sign-in-error"',
    'role="alert"',
    "document.getElementById('sign-in-username')?.focus()",
    'aria-label="Điều hướng chính"',
    'aria-label="Thông báo xác thực"',
    'aria-describedby={state.session.state === ''invalid-credentials'' ? ''sign-in-error'' : undefined}'
)

$missing = @($requiredTokens | Where-Object { $source.IndexOf($_, [StringComparison]::Ordinal) -lt 0 })
if ($missing.Count -gt 0) {
    throw "AppShell accessibility static contract failed; missing: $($missing -join ', ')"
}

$usernameInput = [regex]::Match($source, '(?s)<input id="sign-in-username".*?/>')
$passwordInput = [regex]::Match($source, '(?s)<input id="sign-in-password".*?/>')
if (-not $usernameInput.Success -or -not $passwordInput.Success) {
    throw 'AppShell accessibility static contract failed; sign-in inputs could not be inspected.'
}
if ($usernameInput.Value -notmatch 'aria-describedby=.*sign-in-error' -or
    $passwordInput.Value -notmatch 'aria-describedby=.*sign-in-error') {
    throw 'Invalid-credential error must be described by both sign-in inputs.'
}
$visibleLabels = [regex]::Matches($source, '(?s)<label className="sign-in-label" htmlFor="sign-in-(username|password)">[^<]+</label>')
if ($visibleLabels.Count -ne 2) {
    throw 'Sign-in controls must keep two visible, non-empty labels.'
}
if ($styles -notmatch '(?s)\.sign-in-label\s*\{[^}]*\}' -or
    $styles -match '(?s)\.sign-in-label\s*\{[^}]*display\s*:\s*none') {
    throw 'Sign-in label styling must remain visibly rendered.'
}

Write-Output 'PASS: AppShell accessibility static contract (browser behavior not exercised)'
exit 0

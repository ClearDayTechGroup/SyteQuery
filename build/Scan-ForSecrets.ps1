<#
.SYNOPSIS
    Read-only scan of a folder for things that shouldn't be published: credentials, keys, tenant
    details, and files that are usually local-only. Run it on the NEW public repo folder before the
    first commit (and again before each release).

.PARAMETER Path
    Folder to scan. Defaults to the repository root.

.PARAMETER Terms
    Extra strings you know are sensitive - an old password, your tenant host, a customer name,
    personal email addresses. Matched case-insensitively as plain text. Values are never printed,
    only where they were found.

.EXAMPLE
    ./build/Scan-ForSecrets.ps1 -Path C:\src\SyteQuery-public -Terms 'old-password','csi10x.erpsl'

.NOTES
    Exits with code 1 if anything is found. It reports file and line only - never the matching
    value - so the output is safe to paste into a chat or an issue. It's a safety net, not proof:
    a clean scan doesn't replace reading the diff.
#>
param(
    [string]$Path = (Split-Path $PSScriptRoot -Parent),
    [string[]]$Terms = @()
)

$ErrorActionPreference = 'Stop'
$Path = (Resolve-Path $Path).Path
$skipDirs = '[\\/](\.git|bin|obj|artifacts|\.vs|node_modules|packages)[\\/]'
$textExt = '.cs','.xaml','.csproj','.props','.targets','.json','.xml','.config','.md','.txt','.yml','.yaml','.ps1','.sh','.sln','.slnx','.xshd','.settings','.ini','.env','.sql','.html','.js','.css'

# Files that are usually local-only. Presence alone is worth a look.
$riskyNames = '*.pfx','*.p12','*.pem','*.key','*.snk','secrets.json','appsettings.Production.json','appsettings.*.local.json','.env','.env.*','*.user','*.suo','launchSettings.json','settings.local.json','*.db','*.sqlite','*.sqlite3','*.dll','*.exe','*.pdf','*.bak','*.log'

# name -> regex. Assignments only count when a real-looking literal value follows.
$patterns = [ordered]@{
    'private key block'         = '-----BEGIN (RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----'
    'AWS access key'            = 'AKIA[0-9A-Z]{16}'
    'bearer/auth token'         = '(?i)bearer\s+[A-Za-z0-9\-_\.]{24,}'
    'API key / secret literal'  = '(?i)(api[_-]?key|apikey|secret|client[_-]?secret|token)["'']?\s*[:=]\s*["''](?![<{$%]|your|change|xxx|example|placeholder|\s*["'']|$)[^"'']{12,}["'']'
    'password literal'          = '(?i)(password|pwd|passwd)["'']?\s*[:=]\s*["''](?![<{$%]|your|change|xxx|example|placeholder|\*|\s*["''])[^"'']{4,}["'']'
    'connection string secret'  = '(?i)(Password|Pwd)=[^;"''\s]{3,};'
    'credentialed URL'          = '(?i)https?://[^/\s:@]+:[^/\s:@]+@'
    'GitHub token'              = '\bgh[pousr]_[A-Za-z0-9]{30,}\b'
    'Slack/webhook secret'      = 'hooks\.slack\.com/services/[A-Za-z0-9/]+'
}

$hits = New-Object System.Collections.Generic.List[object]
function Add-Hit($file, $line, $what) {
    $hits.Add([pscustomobject]@{ File = $file.Substring($Path.Length).TrimStart('\'); Line = $line; Problem = $what })
}

$files = Get-ChildItem -Path $Path -Recurse -File -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch $skipDirs -and $_.Name -ne 'Scan-ForSecrets.ps1' }

foreach ($f in $files) {
    foreach ($n in $riskyNames) {
        if ($f.Name -like $n) { Add-Hit $f.FullName '' "risky file type ($n)"; break }
    }

    if ($textExt -notcontains $f.Extension.ToLower() -and $f.Name -notlike '.env*') { continue }
    if ($f.Length -gt 2MB) { continue }

    $lineNo = 0
    foreach ($line in (Get-Content -LiteralPath $f.FullName -ErrorAction SilentlyContinue)) {
        $lineNo++
        if ($line.Length -gt 4000) { continue }   # skip embedded blobs (base64 payloads etc.)
        foreach ($p in $patterns.GetEnumerator()) {
            if ($line -match $p.Value) { Add-Hit $f.FullName $lineNo $p.Key }
        }
        foreach ($t in $Terms) {
            if ($t -and $line.IndexOf($t, [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Hit $f.FullName $lineNo 'matches one of your -Terms' }
        }
    }
}

if ($hits.Count -eq 0) {
    Write-Host "Scanned $($files.Count) files under $Path - nothing found." -ForegroundColor Green
    exit 0
}

Write-Host "Found $($hits.Count) thing(s) to review under ${Path}:" -ForegroundColor Yellow
$hits | Sort-Object File, Line | Format-Table -AutoSize | Out-String -Width 220 | Write-Host
exit 1

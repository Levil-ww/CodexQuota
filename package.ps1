$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$destination = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$archive = Join-Path $destination 'CodexQuota-Windows.zip'
$files = @(
    (Join-Path $PSScriptRoot 'dist/CodexQuota.exe'),
    (Join-Path $PSScriptRoot 'dist/CodexQuota.exe.config'),
    (Join-Path $PSScriptRoot 'README.md'),
    (Join-Path $PSScriptRoot 'LICENSE')
)
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
$digest = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $destination 'SHA256SUMS.txt'), "$digest  CodexQuota-Windows.zip`n", [Text.Encoding]::ASCII)
Write-Host 'Package created: artifacts/CodexQuota-Windows.zip'

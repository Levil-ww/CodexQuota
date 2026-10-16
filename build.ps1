$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }

$source = Join-Path $PSScriptRoot 'source'
$output = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /target:winexe /platform:anycpu /codepage:65001 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /out:"$output\CodexQuota.exe" "$source\Program.cs" "$source\CodexClient.cs" "$source\QuotaCore.cs" "$source\StartupShortcut.cs"
if ($LASTEXITCODE -ne 0) { throw 'Application build failed. Exit any running copy in dist before rebuilding.' }
Copy-Item -LiteralPath "$source\App.config" -Destination "$output\CodexQuota.exe.config" -Force
& $compiler /nologo /platform:anycpu /codepage:65001 /r:System.Web.Extensions.dll /out:"$output\QuotaTests.exe" "$source\QuotaTests.cs" "$source\QuotaCore.cs" "$source\StartupShortcut.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& "$output\QuotaTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
Write-Host 'Build and all tests passed. Output: dist/CodexQuota.exe'

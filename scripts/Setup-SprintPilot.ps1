[CmdletBinding()]
param([int]$Port=5271, [switch]$NoShortcut, [switch]$NoLaunch)
. "$PSScriptRoot\Common.ps1"
$paths = Get-SprintPilotPaths
$localPort = Get-SprintPilotPort $paths $Port
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 10 SDK for your Windows architecture, then run setup again. No Azure resources are needed.' }
$sdks = & dotnet --list-sdks
if (!($sdks | Where-Object { $_ -match '^10\.0\.\d+' })) { throw '.NET 10 SDK is required. Install it or ask your AVD administrator to make it available.' }
if ($null -ne (Get-SprintPilotHealth $localPort)) { throw 'Stop SprintPilot before updating its published files: scripts\Stop-SprintPilot.ps1' }
Push-Location $paths.Repo
try {
    & dotnet restore SprintPilot.sln
    if ($LASTEXITCODE -ne 0) { throw 'Package restore failed. Check access to configured NuGet sources.' }
    & dotnet build SprintPilot.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. Review the compiler output above.' }
    & dotnet run --project tests/SprintPilot.UnitTests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Unit checks failed. Setup stopped.' }
    & dotnet run --project tests/SprintPilot.IntegrationTests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Integration checks failed. Setup stopped.' }
    & dotnet publish src/SprintPilot.Web -c Release --no-restore --no-self-contained -o $paths.Publish
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
} finally { Pop-Location }
@{ Port=$localPort } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $paths.Local 'launcher.json') -Encoding UTF8
if (!$NoShortcut) {
    try {
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'SprintPilot.lnk'))
        $shortcut.TargetPath = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
        $shortcut.Arguments = '-NoProfile -WindowStyle Hidden -File "' + (Join-Path $PSScriptRoot 'Start-SprintPilot.ps1') + '" -Port ' + $localPort
        $shortcut.WorkingDirectory = $paths.Repo
        $shortcut.Description = 'Open SprintPilot sprint workspace'
        $shortcut.Save()
        Write-Host 'Desktop shortcut created. You can pin the shortcut or install the PWA from Edge if your AVD policy permits.'
    } catch { Write-Warning 'Desktop shortcut creation was not permitted. Use Start-SprintPilot.ps1 instead.' }
}
Write-Host 'First run: enter organization, project and PAT in the welcome screen. Windows Credential Manager stores the token.'
Write-Host 'Alternatively set SPRINTPILOT_AZDO_ORGANIZATION, SPRINTPILOT_AZDO_PROJECT and SPRINTPILOT_AZDO_PAT in the launching process environment.'
if (!$NoLaunch) { & "$PSScriptRoot\Start-SprintPilot.ps1" -Port $localPort }

[CmdletBinding()]
param([int]$Port=0)
. "$PSScriptRoot\Common.ps1"
$paths = Get-SprintPilotPaths
$localPort = Get-SprintPilotPort $paths $Port
$health = Get-SprintPilotHealth $localPort
if ($null -eq $health) { Write-Host 'No healthy SprintPilot instance is running on this port. No processes were stopped.'; return }
$session = Get-SprintPilotSession $paths $localPort
$process = Assert-SprintPilotProcess $session $health $paths
Stop-Process -Id $process.Id -ErrorAction Stop
Remove-Item -LiteralPath (Join-Path $paths.Local "session-$localPort.json") -ErrorAction SilentlyContinue
Write-Host 'SprintPilot stopped. No other dotnet processes were touched.'

[CmdletBinding()]
param([int]$Port=0, [switch]$NoShortcut, [switch]$NoBrowser)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"
$paths = Get-SprintPilotPaths

if (!(Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required. Install Git for Windows, then run the updater again.'
}

Push-Location $paths.Repo
try {
    $changes = & git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the Git working tree.' }
    if ($changes) {
        throw 'The repository contains local changes. Commit or stash them before updating so nothing is overwritten.'
    }

    $branch = (& git branch --show-current).Trim()
    if ($branch -ne 'main') {
        throw "The repository is on branch '$branch'. Switch to main before updating."
    }
} finally { Pop-Location }

& "$PSScriptRoot\Stop-SprintPilot.ps1" -Port $Port

Push-Location $paths.Repo
try {
    Write-Host 'Downloading the latest SprintPilot code...'
    & git pull --ff-only origin main
    if ($LASTEXITCODE -ne 0) { throw 'Git pull failed. The existing installation was not rebuilt.' }
} finally { Pop-Location }

Write-Host 'Rebuilding and testing SprintPilot...'
& "$PSScriptRoot\Setup-SprintPilot.ps1" -Port $Port -NoShortcut:$NoShortcut -NoLaunch

Write-Host 'Starting SprintPilot...'
& "$PSScriptRoot\Start-SprintPilot.ps1" -Port $Port -NoBrowser:$NoBrowser

Write-Host 'SprintPilot is up to date and running.'

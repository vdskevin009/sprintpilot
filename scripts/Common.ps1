Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Get-SprintPilotPaths {
    $repo = Split-Path -Parent $PSScriptRoot
    $local = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'SprintPilot'
    New-Item -ItemType Directory -Path $local -Force | Out-Null
    # Only this Windows user and SYSTEM may read session launch capabilities.
    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    foreach ($identity in @($sid, ([Security.Principal.SecurityIdentifier]::new('S-1-5-18')))) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    # Persist only the modified DACL. Set-Acl can also attempt to write the
    # audit descriptor, which requires SeSecurityPrivilege on managed AVDs.
    $directory = [IO.DirectoryInfo]::new($local)
    if ($PSVersionTable.PSEdition -eq 'Desktop') {
        $directory.SetAccessControl($acl)
    } else {
        [IO.FileSystemAclExtensions]::SetAccessControl($directory, $acl)
    }
    return @{ Repo=$repo; Local=$local; Publish=(Join-Path $repo 'artifacts\publish') }
}
function Get-SprintPilotPort($paths, [int]$Requested=0) {
    if ($Requested -ne 0) { $value = $Requested }
    elseif ($env:SPRINTPILOT_PORT) { $value = [int]$env:SPRINTPILOT_PORT }
    elseif (Test-Path (Join-Path $paths.Local 'launcher.json')) { $value = [int](Get-Content (Join-Path $paths.Local 'launcher.json') -Raw | ConvertFrom-Json).Port }
    else { $value = 5271 }
    if ($value -lt 1024 -or $value -gt 65535) { throw 'Port must be between 1024 and 65535.' }
    return $value
}
function Get-SprintPilotHealth([int]$Port) {
    try { return Invoke-RestMethod -Uri "http://localhost:$Port/health" -TimeoutSec 2 } catch { return $null }
}
function Get-SprintPilotSession($paths, [int]$Port) {
    $file = Join-Path $paths.Local "session-$Port.json"
    if (!(Test-Path $file)) { return $null }
    try { return Get-Content $file -Raw | ConvertFrom-Json } catch { return $null }
}
function Assert-SprintPilotProcess($session, $health, $paths) {
    if ($null -eq $session -or $null -eq $health -or $health.application -ne 'SprintPilot' -or $health.processId -ne $session.ProcessId) { throw 'This port is not owned by a verified SprintPilot session. Choose another port with Setup-SprintPilot.ps1 -Port 5272.' }
    $expected = [IO.Path]::GetFullPath($paths.Publish).TrimEnd('\')
    if ([IO.Path]::GetFullPath($session.BaseDirectory).TrimEnd('\') -ne $expected -or [IO.Path]::GetFullPath($health.baseDirectory).TrimEnd('\') -ne $expected) { throw 'A different SprintPilot checkout is using this port. Stop that checkout or choose another port.' }
    $process = Get-Process -Id $session.ProcessId -ErrorAction Stop
    $recorded = [DateTime]::Parse($session.StartTime).ToUniversalTime()
    if ([Math]::Abs(($process.StartTime.ToUniversalTime() - $recorded).TotalSeconds) -gt 1) { throw 'Session process ID has been reused. Refusing to control this process.' }
    $command = Get-CimInstance Win32_Process -Filter "ProcessId = $($session.ProcessId)"
    $dll = Join-Path $paths.Publish 'SprintPilot.Web.dll'
    if ($command.CommandLine -notlike "*$dll*") { throw 'Process command line does not match this SprintPilot application.' }
    return $process
}

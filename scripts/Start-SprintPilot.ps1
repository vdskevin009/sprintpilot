[CmdletBinding()]
param([int]$Port=0, [switch]$NoBrowser)
. "$PSScriptRoot\Common.ps1"
$paths = Get-SprintPilotPaths
$localPort = Get-SprintPilotPort $paths $Port
$mutex = [System.Threading.Mutex]::new($false, "Local\SprintPilot-Launch-$localPort")
$locked = $false
try {
    try { $locked = $mutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $locked = $true }
    if (!$locked) { throw 'SprintPilot is already being launched. Try the shortcut again in a few seconds.' }
    $health = Get-SprintPilotHealth $localPort
    if ($null -eq $health) {
        $dll = Join-Path $paths.Publish 'SprintPilot.Web.dll'
        if (!(Test-Path $dll)) { throw 'Published build not found. Run scripts\Setup-SprintPilot.ps1 first.' }
        $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
        $info = New-Object System.Diagnostics.ProcessStartInfo
        $info.FileName = $dotnet
        $info.Arguments = '"' + $dll + '"'
        $info.WorkingDirectory = $paths.Publish
        $info.UseShellExecute = $false
        $info.CreateNoWindow = $true
        $info.EnvironmentVariables['SPRINTPILOT_PORT'] = [string]$localPort
        $info.EnvironmentVariables['ASPNETCORE_ENVIRONMENT'] = 'Production'
        $started = [System.Diagnostics.Process]::Start($info)
        $deadline = (Get-Date).AddSeconds(45)
        do {
            if ($started.HasExited) { throw 'SprintPilot exited during startup. Check whether the port is in use and whether the .NET 10 ASP.NET Core runtime is installed. Run the published DLL in a terminal for diagnostics.' }
            Start-Sleep -Milliseconds 250
            $health = Get-SprintPilotHealth $localPort
            $session = Get-SprintPilotSession $paths $localPort
            if ($null -ne $health -and $null -ne $session -and $session.ProcessId -eq $started.Id) { break }
        } while ((Get-Date) -lt $deadline)
        if ($null -eq $health) {
            if (!$started.HasExited) { $started.Kill() }
            throw 'SprintPilot did not become healthy within 45 seconds. Check local firewall and port settings.'
        }
    }
    $session = Get-SprintPilotSession $paths $localPort
    $null = Assert-SprintPilotProcess $session $health $paths
    if (!$NoBrowser) { Start-Process "http://localhost:$localPort/launch#$($session.Key)" }
    Write-Host "SprintPilot is running at http://localhost:$localPort"
} catch {
    Write-Error $_.Exception.Message
} finally {
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}

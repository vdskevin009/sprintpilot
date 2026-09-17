. "$PSScriptRoot\Common.ps1"
$publish = 'C:\Apps with spaces\SprintPilot\artifacts\publish'
$exe = Join-Path $publish 'SprintPilot.Web.exe'
$dll = Join-Path $publish 'SprintPilot.Web.dll'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
function Assert-Match($image, $command, $expected) {
    if ((Test-SprintPilotProcessImage $image $command $publish) -ne $expected) { throw 'Process identity regression check failed.' }
}
Assert-Match $exe 'SprintPilot.Web.exe' $true
Assert-Match $exe ('"' + $exe + '"') $true
Assert-Match $exe.ToUpperInvariant() '' $true
Assert-Match $dotnet ('"' + $dotnet + '" "' + $dll + '"') $true
Assert-Match 'C:\Other\SprintPilot.Web.exe' 'SprintPilot.Web.exe' $false
Assert-Match $dotnet ('dotnet.exe "' + $dll + '.backup"') $false
Assert-Match $dotnet ('dotnet.exe other.dll --note "' + $dll + '"') $false
Assert-Match 'C:\Other\tool.exe' ('tool.exe "' + $dll + '"') $false
Assert-Match '' ('dotnet.exe "' + $dll + '"') $false
Assert-Match $dotnet '' $false
Write-Host 'Ten launcher process identity checks passed.'

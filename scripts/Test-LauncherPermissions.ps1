. "$PSScriptRoot\Common.ps1"
$paths = Get-SprintPilotPaths
$before = (Get-Acl -LiteralPath $paths.Local).Owner
$again = Get-SprintPilotPaths
$acl = Get-Acl -LiteralPath $again.Local
if ($acl.Owner -ne $before) { throw 'Owner changed.' }
if (!$acl.AreAccessRulesProtected) { throw 'Directory permissions are not protected.' }
$allowed = @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value, 'S-1-5-18')
$rules = @($acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]))
if ($rules.Count -ne 2) { throw 'Unexpected directory access rules.' }
foreach ($rule in $rules) {
    if ($rule.IdentityReference.Value -notin $allowed -or $rule.AccessControlType -ne 'Allow' -or $rule.FileSystemRights -ne 'FullControl') {
        throw 'Unexpected access grant.'
    }
}
$probe = Join-Path $paths.Local ('permission-check-' + [Guid]::NewGuid() + '.txt')
try {
    'probe' | Set-Content -LiteralPath $probe
    if ((Get-Content -LiteralPath $probe) -ne 'probe') { throw 'Unable to read local app data.' }
} finally { Remove-Item -LiteralPath $probe -ErrorAction SilentlyContinue }
Write-Host 'Launcher permission checks passed.'

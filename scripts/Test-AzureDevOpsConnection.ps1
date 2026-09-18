[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]{0,100}$')]
    [string]$Organization,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Project
)

$ErrorActionPreference = 'Stop'
$securePat = Read-Host 'Enter Azure DevOps PAT' -AsSecureString
$patPointer = [IntPtr]::Zero
$plainPat = $null

function Test-Endpoint {
    param(
        [Parameter(Mandatory = $true)] [string]$Name,
        [Parameter(Mandatory = $true)] [string]$Uri,
        [Parameter(Mandatory = $true)] [hashtable]$Headers
    )

    try {
        $response = Invoke-WebRequest -Uri $Uri -Headers $Headers -UseBasicParsing
        [pscustomobject]@{
            Endpoint = $Name
            Success = $true
            Status = [int]$response.StatusCode
            Meaning = 'Request succeeded.'
        }
    }
    catch {
        $status = $null
        if ($null -ne $_.Exception.Response) {
            try { $status = [int]$_.Exception.Response.StatusCode } catch { }
        }

        $meaning = switch ($status) {
            400 { 'Azure DevOps rejected the request. Verify the organization and project values.' }
            401 { 'Authentication failed. The PAT may be invalid or expired.' }
            403 { 'Authentication succeeded, but the PAT or user lacks permission.' }
            404 { 'The organization or project was not found, or access is hidden.' }
            407 { 'The AVD proxy requires authentication.' }
            429 { 'Azure DevOps is throttling requests. Try again later.' }
            default { 'The request failed. Check VPN, proxy, DNS, TLS, and Azure DevOps availability.' }
        }

        [pscustomobject]@{
            Endpoint = $Name
            Success = $false
            Status = if ($null -eq $status) { 'No HTTP response' } else { $status }
            Meaning = $meaning
        }
    }
}

try {
    $patPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePat)
    $plainPat = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($patPointer)
    $basicValue = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(":" + $plainPat))
    $headers = @{ Authorization = 'Basic ' + $basicValue }

    $escapedOrganization = [Uri]::EscapeDataString($Organization.Trim())
    $escapedProject = [Uri]::EscapeDataString($Project.Trim())
    $baseUri = "https://dev.azure.com/$escapedOrganization"

    $results = @(
        Test-Endpoint -Name 'Project access' -Uri "$baseUri/_apis/projects/$escapedProject`?api-version=7.1" -Headers $headers
        Test-Endpoint -Name 'Authentication identity' -Uri "$baseUri/_apis/connectionData?connectOptions=1&lastChangeId=-1&lastChangeId64=-1&api-version=7.1" -Headers $headers
    )

    $results | Format-Table -AutoSize
    if ($results.Success -contains $false) { exit 1 }
    Write-Host 'Azure DevOps connection checks passed.' -ForegroundColor Green
}
finally {
    $plainPat = $null
    $headers = $null
    if ($patPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($patPointer)
    }
}

param([Parameter(Mandatory = $true)][string]$Executable)
$ErrorActionPreference = 'Stop'
$executablePath = (Resolve-Path $Executable).Path
$reportDirectory = Join-Path $env:TEMP ("NeoPad-Diagnostics-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $reportDirectory | Out-Null
$reportPath = Join-Path $reportDirectory 'smoke-result.txt'

# Record platform and executable versions without enumerating environment variables or user documents.
@(
    "Windows: $([Environment]::OSVersion.VersionString)"
    "Architecture: $([Runtime.InteropServices.RuntimeInformation]::OSArchitecture)"
    "Application: $((Get-Item $executablePath).VersionInfo.FileVersion)"
) | Set-Content (Join-Path $reportDirectory 'platform.txt')

# Run the built-in test against temporary sample files, leaving real user documents untouched.
$process = Start-Process -FilePath $executablePath -ArgumentList "--smoke-test --smoke-output `"$reportPath`"" -PassThru
if (-not $process.WaitForExit(90000)) {
    $process.Kill()
    'FAILED: Windows UI test timed out after 90 seconds.' | Set-Content $reportPath
}
if (Test-Path $reportPath) { Get-Content $reportPath }
else { 'FAILED: Application exited without writing a report.' | Set-Content $reportPath }
Write-Host "Diagnostics saved to: $reportDirectory"
if ($process.ExitCode -ne 0) { exit 1 }

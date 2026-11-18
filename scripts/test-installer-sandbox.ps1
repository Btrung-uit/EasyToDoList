[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Installer,
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [Parameter(Mandatory = $true)][string]$CompilerPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
$Installer = (Resolve-Path -LiteralPath $Installer).Path
$PublishDirectory = (Resolve-Path -LiteralPath $PublishDirectory).Path
$CompilerPath = (Resolve-Path -LiteralPath $CompilerPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $PublishDirectory 'coreclr.dll'))) { throw 'Pass the self-contained publish directory printed by package-release.ps1.' }
$runDirectory = Join-Path $repoRoot ('artifacts/sandbox/' + [guid]::NewGuid().ToString('N'))
$inputDirectory = Join-Path $runDirectory 'input'
$evidenceDirectory = Join-Path $runDirectory 'evidence'
New-Item -ItemType Directory -Path $inputDirectory, $evidenceDirectory -Force | Out-Null
Copy-Item -LiteralPath $Installer -Destination (Join-Path $inputDirectory 'candidate.exe')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/testing/m12-installer-smoke.ps1') -Destination $inputDirectory
& $CompilerPath '/Qp' '/DAppVersion=0.0.0' '/DFileVersion=0.0.0' "/DPublishDir=$PublishDirectory" "/DInstallerOutputDir=$inputDirectory" (Join-Path $repoRoot 'installer/EasyToDoList.iss')
if ($LASTEXITCODE -ne 0) { throw 'Cannot compile the previous-registration fixture.' }
# This fixture uses the candidate payload with an older installer registration; schema upgrade is tested separately.
$candidateVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Installer).ProductVersion.Trim()
$guest = @'
$ErrorActionPreference = 'Stop'
try {
    & C:\Input\m12-installer-smoke.ps1 -Installer C:\Input\candidate.exe -PreviousInstaller C:\Input\EasyToDoList-0.0.0-win-x64-setup.exe -ExpectedVersion '__VERSION__' *> C:\Evidence\console.txt
}
finally {
    if (Test-Path "$env:LOCALAPPDATA\EasyToDoList") { Copy-Item -LiteralPath "$env:LOCALAPPDATA\EasyToDoList" -Destination C:\Evidence\user-data -Recurse -Force }
    & shutdown.exe /s /t 0
}
'@
$guest.Replace('__VERSION__', $candidateVersion) | Set-Content -LiteralPath (Join-Path $inputDirectory 'run.ps1') -Encoding UTF8
$inputXml = [System.Security.SecurityElement]::Escape($inputDirectory)
$evidenceXml = [System.Security.SecurityElement]::Escape($evidenceDirectory)
$configuration = @"
<Configuration>
  <VGpu>Disable</VGpu><Networking>Disable</Networking><ClipboardRedirection>Disable</ClipboardRedirection>
  <MappedFolders>
    <MappedFolder><HostFolder>$inputXml</HostFolder><SandboxFolder>C:\Input</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$evidenceXml</HostFolder><SandboxFolder>C:\Evidence</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Input\run.ps1</Command></LogonCommand>
</Configuration>
"@
$configurationPath = Join-Path $runDirectory 'release-smoke.wsb'
$configuration | Set-Content -LiteralPath $configurationPath -Encoding UTF8
Start-Process -FilePath "$env:WINDIR\System32\WindowsSandbox.exe" -ArgumentList ('"' + $configurationPath + '"') -WindowStyle Hidden | Out-Null
Write-Output "Sandbox evidence: $evidenceDirectory"
Write-Output 'The guest shuts down after the smoke. Inspect result.json; launching the VM alone is not a passing result.'

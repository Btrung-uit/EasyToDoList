[CmdletBinding()]
param(
    [string]$Version,
    [string]$CompilerPath,
    [switch]$NoRestore
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent

function Invoke-Checked([string]$ToolPath, [string[]]$Arguments) {
    & $ToolPath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$ToolPath failed with exit code $LASTEXITCODE" }
}

Push-Location $repoRoot
try {
    if (-not $Version) {
        $Version = (& dotnet msbuild src/EasyToDoList.App/EasyToDoList.App.csproj -getProperty:Version -nologo | Out-String).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Cannot read the application version.' }
    }
    if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
        throw 'Version must be major.minor.patch with an optional prerelease suffix.'
    }
    $fileVersion = $Version.Split('-')[0]
    $numericVersion = [version]$fileVersion
    if ($numericVersion.Major -gt 65535 -or $numericVersion.Minor -gt 65535 -or $numericVersion.Build -gt 65535) {
        throw 'Version components must fit Windows version fields (0..65535).'
    }
    if (-not $CompilerPath) {
        $CompilerPath = @(
            (Join-Path $repoRoot 'artifacts/tools/InnoSetup/ISCC.exe'),
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
        throw 'Inno Setup 6 ISCC.exe is required. Install it or pass -CompilerPath.'
    }
    $CompilerPath = (Resolve-Path -LiteralPath $CompilerPath).Path
    # A fresh staging directory prevents old publish files from entering a new installer.
    $stage = Join-Path $repoRoot ('artifacts/staging/' + [guid]::NewGuid().ToString('N'))
    $publish = Join-Path $stage 'publish'
    $output = Join-Path $repoRoot "artifacts/release/$Version"
    New-Item -ItemType Directory -Path $publish, $output -Force | Out-Null
    if (-not $NoRestore) {
        Invoke-Checked dotnet @('restore', 'src/EasyToDoList.App/EasyToDoList.App.csproj', '-r', 'win-x64', '-p:SelfContained=true')
    }
    Invoke-Checked dotnet @('publish', 'src/EasyToDoList.App/EasyToDoList.App.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore', '-m:1', '-p:UseSharedCompilation=false', '-p:DebugType=None', '-p:DebugSymbols=false', "-p:Version=$Version", '-o', $publish, '--verbosity', 'minimal')
    foreach ($required in @('EasyToDoList.App.exe', 'EasyToDoList.App.dll', 'EasyToDoList.App.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'PresentationFramework.dll', 'e_sqlite3.dll', 'README.md', 'ASSET-LICENSES.md', 'third-party-notices/SQLitePCLRaw-LICENSE.txt', 'third-party-notices/SQLitePCLRaw-NOTICE.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $publish $required) -PathType Leaf)) { throw "Incomplete payload: $required is missing." }
    }
    $runtime = Get-Content -LiteralPath (Join-Path $publish 'EasyToDoList.App.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (-not $runtime.runtimeOptions.PSObject.Properties['includedFrameworks']) { throw 'Payload is not self-contained.' }
    $assets = Get-Content -LiteralPath 'src/EasyToDoList.App/obj/project.assets.json' -Raw | ConvertFrom-Json
    foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
        $packageName = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
        $package = $assets.packageFolders.PSObject.Properties.Name | ForEach-Object {
            Join-Path $_ ($packageName + '/' + $framework.version)
        } | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
        if (-not $package) { throw "Runtime license package not found: $packageName" }
        $noticeDirectory = Join-Path $publish ('runtime-notices/' + $framework.name)
        New-Item -ItemType Directory -Path $noticeDirectory -Force | Out-Null
        $notices = @(Get-ChildItem -LiteralPath $package -File | Where-Object { $_.Name -match '^LICENSE(\.TXT)?$|^THIRD-PARTY-NOTICES\.TXT$' })
        if ($notices.Count -eq 0) { throw "Runtime license missing: $packageName" }
        $notices | Copy-Item -Destination $noticeDirectory
    }
    if (Get-ChildItem -LiteralPath $publish -Recurse -File -Include '*.db','*.db-wal','*.db-shm') { throw 'A user database must never be included in the payload.' }
    Invoke-Checked $CompilerPath @('/Qp', "/DAppVersion=$Version", "/DFileVersion=$fileVersion", "/DPublishDir=$publish", "/DInstallerOutputDir=$stage", 'installer/EasyToDoList.iss')
    $archive = Join-Path $stage "EasyToDoList-$Version-win-x64-portable.zip"
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive -CompressionLevel Optimal
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $archivedFiles = @($zip.Entries | Where-Object { $_.Name })
        if ($archivedFiles.Count -ne @(Get-ChildItem -LiteralPath $publish -File -Recurse).Count) {
            throw 'Portable archive is missing published files.'
        }
    }
    finally { $zip.Dispose() }
    $installer = Join-Path $stage "EasyToDoList-$Version-win-x64-setup.exe"
    if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'Compiler did not create the expected installer.' }
    $artifacts = @($installer, $archive)
    $hashes = foreach ($artifact in $artifacts) { '{0}  {1}' -f (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $artifact -Leaf) }
    $hashes | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding ascii
    foreach ($artifact in $artifacts + (Join-Path $stage 'SHA256SUMS.txt')) { Copy-Item -LiteralPath $artifact -Destination $output -Force }
    Write-Output "Release artifacts: $output"
    Write-Output "Verified self-contained payload: $publish"
}
finally { Pop-Location }

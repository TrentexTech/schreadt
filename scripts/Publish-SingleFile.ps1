[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ProjectPath,

    [Parameter(Mandatory)]
    [string] $ApplicationName,

    [string] $Runtime = 'win-x64',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $OutputDirectory,

    [switch] $FrameworkDependent,

    [switch] $NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedProjectPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ProjectPath))
if (-not (Test-Path -LiteralPath $resolvedProjectPath -PathType Leaf)) {
    throw "Project file was not found: '$resolvedProjectPath'."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $resolvedOutputDirectory = Join-Path $repositoryRoot "artifacts\publish\$ApplicationName\$Runtime"
}
elseif ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    $resolvedOutputDirectory = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}

$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
$publishArguments = @(
    'publish'
    $resolvedProjectPath
    '--configuration'
    $Configuration
    '--runtime'
    $Runtime
    '--self-contained'
    $selfContained
    '-p:PublishSingleFile=true'
    '-p:IncludeNativeLibrariesForSelfExtract=true'
    '-p:DebugType=None'
    '-p:DebugSymbols=false'
    '--output'
    $resolvedOutputDirectory
)

if ($NoRestore) {
    $publishArguments += '--no-restore'
}

$dotnet = Get-Command dotnet -ErrorAction Stop
Write-Host "Publishing $ApplicationName for $Runtime..."
Write-Host "  Configuration:  $Configuration"
Write-Host "  Self-contained: $selfContained"
Write-Host "  Output:         $resolvedOutputDirectory"

& $dotnet.Source @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for '$ApplicationName' with exit code $LASTEXITCODE."
}

$executableName = if ($Runtime.StartsWith('win-', [System.StringComparison]::OrdinalIgnoreCase)) {
    "$ApplicationName.exe"
}
else {
    $ApplicationName
}
$executablePath = Join-Path $resolvedOutputDirectory $executableName
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Publish succeeded but the expected executable was not found: '$executablePath'."
}

$projectDirectory = Split-Path -Parent $resolvedProjectPath
$missingContent = [System.Collections.Generic.List[string]]::new()
foreach ($contentDirectoryName in @('config', 'assets')) {
    $sourceContentDirectory = Join-Path $projectDirectory $contentDirectoryName
    if (-not (Test-Path -LiteralPath $sourceContentDirectory -PathType Container)) {
        continue
    }

    Get-ChildItem -LiteralPath $sourceContentDirectory -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Substring($sourceContentDirectory.Length).TrimStart('\', '/')
        $publishedPath = Join-Path (Join-Path $resolvedOutputDirectory $contentDirectoryName) $relativePath
        if (-not (Test-Path -LiteralPath $publishedPath -PathType Leaf)) {
            $missingContent.Add("$contentDirectoryName\$relativePath")
        }
    }
}

if ($missingContent.Count -gt 0) {
    throw "Publish output is missing required content: $($missingContent -join ', ')."
}

Write-Host "Published successfully: $executablePath" -ForegroundColor Green

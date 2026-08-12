[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [string] $ArtifactsDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-SafeRelativePath {
    param(
        [Parameter(Mandatory)]
        [string] $BasePath,

        [Parameter(Mandatory)]
        [string] $Path
    )

    $relativePath = [System.IO.Path]::GetRelativePath($BasePath, $Path)
    if ([System.IO.Path]::IsPathRooted($relativePath) -or
        $relativePath -eq '..' -or
        $relativePath.StartsWith("..$([System.IO.Path]::DirectorySeparatorChar)", [System.StringComparison]::Ordinal)) {
        throw "Path '$Path' is outside '$BasePath'."
    }

    return $relativePath
}

function Get-SafeArchiveEntryPath {
    param(
        [Parameter(Mandatory)]
        [string] $EntryName
    )

    $normalizedPath = $EntryName.Replace('\', '/').TrimStart('/')
    $segments = $normalizedPath.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($segments.Count -eq 0 -or $segments.Where({ $_ -eq '.' -or $_ -eq '..' }).Count -gt 0) {
        throw "Package contains an unsafe log path: '$EntryName'."
    }

    return [System.IO.Path]::Combine($segments)
}

function Copy-StreamWithHash {
    param(
        [Parameter(Mandatory)]
        [System.IO.Stream] $Source,

        [Parameter(Mandatory)]
        [string] $DestinationPath
    )

    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($DestinationPath)) | Out-Null
    $destination = [System.IO.File]::Open(
        $DestinationPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    $hash = [System.Security.Cryptography.IncrementalHash]::CreateHash(
        [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $buffer = [byte[]]::new(81920)
    $length = 0L

    try {
        while (($read = $Source.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $destination.Write($buffer, 0, $read)
            $hash.AppendData($buffer, 0, $read)
            $length += $read
        }

        return [pscustomobject]@{
            Length = $length
            Sha256 = [System.Convert]::ToHexString($hash.GetHashAndReset())
        }
    }
    finally {
        $hash.Dispose()
        $destination.Dispose()
    }
}

function Test-IsLogPath {
    param([Parameter(Mandatory)][string] $Path)

    return [System.IO.Path]::GetExtension($Path).Equals(
        '.log',
        [System.StringComparison]::OrdinalIgnoreCase)
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    $resolvedArtifactsDirectory = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
}
elseif ([System.IO.Path]::IsPathRooted($ArtifactsDirectory)) {
    $resolvedArtifactsDirectory = [System.IO.Path]::GetFullPath($ArtifactsDirectory)
}
else {
    $resolvedArtifactsDirectory = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ArtifactsDirectory))
}

$cleanupTargets = [System.Collections.Generic.List[string]]::new()
foreach ($directoryName in @('publish', 'packages')) {
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $resolvedArtifactsDirectory $directoryName))
    if ([System.IO.Path]::GetDirectoryName($candidate) -ne $resolvedArtifactsDirectory) {
        throw "Refusing to clean unexpected path '$candidate'."
    }

    if (Test-Path -LiteralPath $candidate -PathType Container) {
        $cleanupTargets.Add($candidate)
    }
}

if ($cleanupTargets.Count -eq 0) {
    Write-Host "No local packaged or published artifacts found under '$resolvedArtifactsDirectory'."
    return
}

if (-not $PSCmdlet.ShouldProcess(
        ($cleanupTargets -join ', '),
        'archive every log and remove the local packaged and published artifacts')) {
    return
}

$archiveId = "{0}-{1}" -f [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfffZ'),
    [System.Guid]::NewGuid().ToString('N').Substring(0, 8)
$archiveRoot = Join-Path (Join-Path $resolvedArtifactsDirectory 'logs') 'archives'
$finalArchiveDirectory = Join-Path $archiveRoot $archiveId
$stagingArchiveDirectory = "$finalArchiveDirectory.incomplete"
$archivedLogs = [System.Collections.Generic.List[object]]::new()
$archiveCommitted = $false

try {
    [System.IO.Directory]::CreateDirectory($stagingArchiveDirectory) | Out-Null

    foreach ($target in $cleanupTargets) {
        $targetName = [System.IO.Path]::GetFileName($target)
        foreach ($logFile in Get-ChildItem -LiteralPath $target -Recurse -File | Where-Object {
                Test-IsLogPath $_.Name
            }) {
            $relativeSourcePath = Get-SafeRelativePath -BasePath $target -Path $logFile.FullName
            $archiveRelativePath = Join-Path $targetName $relativeSourcePath
            $destinationPath = Join-Path $stagingArchiveDirectory $archiveRelativePath
            [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destinationPath)) | Out-Null
            Copy-Item -LiteralPath $logFile.FullName -Destination $destinationPath

            $sourceHash = (Get-FileHash -LiteralPath $logFile.FullName -Algorithm SHA256).Hash
            $destinationHash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
            if ($sourceHash -ne $destinationHash) {
                throw "Archived log verification failed for '$($logFile.FullName)'."
            }

            [System.IO.File]::SetLastWriteTimeUtc($destinationPath, $logFile.LastWriteTimeUtc)
            $archivedLogs.Add([pscustomobject]@{
                    Origin = 'loose-file'
                    Source = $logFile.FullName
                    ArchivedPath = $archiveRelativePath
                    Length = $logFile.Length
                    Sha256 = $sourceHash
                })
        }
    }

    $packagesDirectory = Join-Path $resolvedArtifactsDirectory 'packages'
    if (Test-Path -LiteralPath $packagesDirectory -PathType Container) {
        foreach ($packageFile in Get-ChildItem -LiteralPath $packagesDirectory -Recurse -File) {
            $packageName = $packageFile.Name
            $isZip = $packageName.EndsWith('.zip', [System.StringComparison]::OrdinalIgnoreCase)
            $isTar = $packageName.EndsWith('.tar', [System.StringComparison]::OrdinalIgnoreCase)
            $isCompressedTar = $packageName.EndsWith('.tar.gz', [System.StringComparison]::OrdinalIgnoreCase) -or
                $packageName.EndsWith('.tgz', [System.StringComparison]::OrdinalIgnoreCase)
            if (-not ($isZip -or $isTar -or $isCompressedTar)) {
                continue
            }

            $relativePackagePath = Get-SafeRelativePath -BasePath $packagesDirectory -Path $packageFile.FullName
            $packageArchiveRoot = Join-Path 'packages' $relativePackagePath

            if ($isZip) {
                $package = [System.IO.Compression.ZipFile]::OpenRead($packageFile.FullName)
                try {
                    foreach ($entry in $package.Entries) {
                        if (-not (Test-IsLogPath $entry.FullName)) {
                            continue
                        }

                        $entryPath = Get-SafeArchiveEntryPath $entry.FullName
                        $archiveRelativePath = Join-Path $packageArchiveRoot $entryPath
                        $destinationPath = Join-Path $stagingArchiveDirectory $archiveRelativePath
                        $entryStream = $entry.Open()
                        try {
                            $copyResult = Copy-StreamWithHash -Source $entryStream -DestinationPath $destinationPath
                        }
                        finally {
                            $entryStream.Dispose()
                        }

                        if ($entry.LastWriteTime -ne [DateTimeOffset]::MinValue) {
                            [System.IO.File]::SetLastWriteTimeUtc($destinationPath, $entry.LastWriteTime.UtcDateTime)
                        }
                        $archivedLogs.Add([pscustomobject]@{
                                Origin = 'zip-package'
                                Source = "$($packageFile.FullName)::$($entry.FullName)"
                                ArchivedPath = $archiveRelativePath
                                Length = $copyResult.Length
                                Sha256 = $copyResult.Sha256
                            })
                    }
                }
                finally {
                    $package.Dispose()
                }
                continue
            }

            $packageStream = [System.IO.File]::OpenRead($packageFile.FullName)
            $archiveStream = $packageStream
            if ($isCompressedTar) {
                $archiveStream = [System.IO.Compression.GZipStream]::new(
                    $packageStream,
                    [System.IO.Compression.CompressionMode]::Decompress,
                    $true)
            }
            $tarReader = [System.Formats.Tar.TarReader]::new($archiveStream, $true)
            try {
                while (($entry = $tarReader.GetNextEntry()) -ne $null) {
                    if ($null -eq $entry.DataStream -or -not (Test-IsLogPath $entry.Name)) {
                        continue
                    }

                    $entryPath = Get-SafeArchiveEntryPath $entry.Name
                    $archiveRelativePath = Join-Path $packageArchiveRoot $entryPath
                    $destinationPath = Join-Path $stagingArchiveDirectory $archiveRelativePath
                    $copyResult = Copy-StreamWithHash -Source $entry.DataStream -DestinationPath $destinationPath
                    [System.IO.File]::SetLastWriteTimeUtc($destinationPath, $entry.ModificationTime.UtcDateTime)
                    $archivedLogs.Add([pscustomobject]@{
                            Origin = 'tar-package'
                            Source = "$($packageFile.FullName)::$($entry.Name)"
                            ArchivedPath = $archiveRelativePath
                            Length = $copyResult.Length
                            Sha256 = $copyResult.Sha256
                        })
                }
            }
            finally {
                $tarReader.Dispose()
                if ($archiveStream -ne $packageStream) {
                    $archiveStream.Dispose()
                }
                $packageStream.Dispose()
            }
        }
    }

    if ($archivedLogs.Count -gt 0) {
        $manifest = [ordered]@{
            ArchiveId = $archiveId
            CreatedUtc = [DateTime]::UtcNow.ToString('O')
            ArtifactsDirectory = $resolvedArtifactsDirectory
            RemovedDirectories = @($cleanupTargets)
            Logs = @($archivedLogs)
        }
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (
            Join-Path $stagingArchiveDirectory 'manifest.json') -Encoding utf8
        Move-Item -LiteralPath $stagingArchiveDirectory -Destination $finalArchiveDirectory
        $archiveCommitted = $true
    }
    else {
        Remove-Item -LiteralPath $stagingArchiveDirectory -Recurse -Force
    }

    foreach ($target in $cleanupTargets) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}
catch {
    if (-not $archiveCommitted -and (Test-Path -LiteralPath $stagingArchiveDirectory -PathType Container)) {
        Remove-Item -LiteralPath $stagingArchiveDirectory -Recurse -Force
    }
    throw
}

if ($archiveCommitted) {
    Write-Host "Preserved $($archivedLogs.Count) log(s) in '$finalArchiveDirectory'." -ForegroundColor Green
}
else {
    Write-Host 'No logs were found in the local artifacts.'
}
Write-Host "Removed $($cleanupTargets.Count) artifact director$(if ($cleanupTargets.Count -eq 1) { 'y' } else { 'ies' })." -ForegroundColor Green

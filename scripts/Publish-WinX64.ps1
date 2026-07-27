[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')]
    [string]$Version = '0.3.1',

    [Parameter()]
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

function Assert-RepositoryChildPath {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Description
    )

    $resolvedPath = [IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith(
            $script:RepositoryPrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to manage $Description outside the repository: $resolvedPath"
    }

    return $resolvedPath
}

function Get-PeMachine {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $stream = [IO.File]::OpenRead($Path)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) {
            throw "The executable does not have a valid DOS header: $Path"
        }

        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 0 -or $peOffset -gt ($stream.Length - 6)) {
            throw "The executable has an invalid PE header offset: $Path"
        }

        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) {
            throw "The executable does not have a valid PE signature: $Path"
        }

        return $reader.ReadUInt16()
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Get-ForbiddenPackageReason {
    param(
        [Parameter(Mandatory)]
        [string]$RelativePath
    )

    $normalizedPath = $RelativePath.Replace('\', '/').Trim('/')
    if ([string]::IsNullOrWhiteSpace($normalizedPath)) {
        return $null
    }

    $segments = $normalizedPath.Split('/')
    foreach ($segment in $segments) {
        if ($segment -ieq '.git' -or
            $segment -ieq '.dotnet' -or
            $segment -ieq '.secrets' -or
            $segment -ieq 'test' -or
            $segment -ieq 'tests' -or
            $segment -ieq 'TestResults' -or
            $segment -ieq 'EBWebView' -or
            $segment -ieq 'WebView2' -or
            $segment.EndsWith('.WebView2', [StringComparison]::OrdinalIgnoreCase)) {
            return "forbidden directory '$segment'"
        }
    }

    $fileName = $segments[-1]
    $extension = [IO.Path]::GetExtension($fileName)

    if ($fileName -ieq '.gitignore' -or
        $fileName -ieq '.gitattributes' -or
        $fileName -ieq '.env' -or
        $fileName.StartsWith('.env.', [StringComparison]::OrdinalIgnoreCase) -or
        $fileName -ieq 'credentials.json' -or
        $fileName -ieq 'secrets.json' -or
        $fileName -ieq 'settings.json' -or
        $fileName -ieq 'user.config' -or
        $fileName -ilike 'appsettings.*.local.json') {
        return "forbidden configuration or secret file '$fileName'"
    }

    if ($extension -in @(
            '.cs',
            '.xaml',
            '.csproj',
            '.vb',
            '.fs',
            '.fsx',
            '.c',
            '.cc',
            '.cpp',
            '.h',
            '.hpp',
            '.sln',
            '.props',
            '.targets',
            '.razor',
            '.cshtml',
            '.resx',
            '.ps1',
            '.sh',
            '.cmd',
            '.bat',
            '.pdb',
            '.user',
            '.suo',
            '.pfx',
            '.p12',
            '.snk',
            '.pem',
            '.key',
            '.zip',
            '.rar',
            '.7z',
            '.tar',
            '.gz',
            '.tgz',
            '.bz2',
            '.xz')) {
        return "forbidden file type '$extension'"
    }

    if ($fileName -imatch '(^|[._-])(testhost|tests?)([._-]|$)' -or
        $fileName -imatch '\.Tests\.(dll|exe|deps\.json|runtimeconfig\.json)$') {
        return "test file '$fileName'"
    }

    return $null
}

function Assert-PublishDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$Directory
    )

    $requiredRootFiles = @(
        'SvgLiveEditor.exe',
        'SvgLiveEditor.dll',
        'coreclr.dll',
        'hostfxr.dll',
        'hostpolicy.dll',
        'System.Private.CoreLib.dll',
        'WebView2Loader.dll'
    )

    foreach ($requiredRootFile in $requiredRootFiles) {
        $requiredPath = Join-Path $Directory $requiredRootFile
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "The self-contained package is missing required root file '$requiredRootFile'."
        }
    }

    $machine = Get-PeMachine -Path (Join-Path $Directory 'SvgLiveEditor.exe')
    if ($machine -ne 0x8664) {
        throw ('SvgLiveEditor.exe is not an x64 PE executable (machine 0x{0:X4}).' -f $machine)
    }

    foreach ($item in Get-ChildItem -LiteralPath $Directory -Recurse -Force) {
        $relativePath = $item.FullName.Substring($Directory.Length).TrimStart('\', '/')
        $reason = Get-ForbiddenPackageReason -RelativePath $relativePath
        if ($null -ne $reason) {
            throw "Refusing to package $reason at '$relativePath'."
        }
    }
}

function Assert-ZipPackage {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $requiredRootEntries = @(
            'SvgLiveEditor.exe',
            'SvgLiveEditor.dll',
            'coreclr.dll',
            'hostfxr.dll',
            'hostpolicy.dll',
            'System.Private.CoreLib.dll',
            'WebView2Loader.dll'
        )
        foreach ($requiredRootEntry in $requiredRootEntries) {
            $entry = $archive.Entries |
              Where-Object { $_.FullName -ceq $requiredRootEntry } |
              Select-Object -First 1
            if ($null -eq $entry) {
                throw "The ZIP is missing required root entry '$requiredRootEntry'."
            }
        }

        foreach ($entry in $archive.Entries) {
            $reason = Get-ForbiddenPackageReason -RelativePath $entry.FullName
            if ($null -ne $reason) {
                throw "The ZIP contains $reason at '$($entry.FullName)'."
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

$resolvedRepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot).
  TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $resolvedRepositoryRoot -PathType Container)) {
    throw "Repository root was not found: $resolvedRepositoryRoot"
}

$script:RepositoryPrefix = $resolvedRepositoryRoot + [IO.Path]::DirectorySeparatorChar
$projectPath = Assert-RepositoryChildPath `
  -Path (Join-Path $resolvedRepositoryRoot 'src\SvgLiveEditor\SvgLiveEditor.csproj') `
  -Description 'the project file'
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "SvgLiveEditor project was not found: $projectPath"
}

$resolvedPublishDirectory = Assert-RepositoryChildPath `
  -Path (Join-Path $resolvedRepositoryRoot 'dist\win-x64') `
  -Description 'publish output'
$resolvedCandidateDirectory = Assert-RepositoryChildPath `
  -Path (Join-Path $resolvedRepositoryRoot 'dist\win-x64-candidate') `
  -Description 'candidate publish output'
$resolvedStagingDirectory = Assert-RepositoryChildPath `
  -Path (Join-Path $resolvedRepositoryRoot "dist\.win-x64-staging-$PID") `
  -Description 'staging publish output'
$resolvedReleaseDirectory = Assert-RepositoryChildPath `
  -Path (Join-Path $resolvedRepositoryRoot 'releases') `
  -Description 'release output'

$archiveFileName = "SvgLiveEditor-v$Version-win-x64.zip"
$checksumFileName = "SvgLiveEditor-v$Version-win-x64.sha256"
$archivePath = Assert-RepositoryChildPath `
  -Path (Join-Path $resolvedReleaseDirectory $archiveFileName) `
  -Description 'the release archive'
$checksumPath = Assert-RepositoryChildPath `
  -Path (Join-Path $resolvedReleaseDirectory $checksumFileName) `
  -Description 'the checksum file'

$localDotnet = Join-Path $resolvedRepositoryRoot '.dotnet\dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }

New-Item -ItemType Directory -Force -Path $resolvedReleaseDirectory | Out-Null

if (Test-Path -LiteralPath $resolvedStagingDirectory) {
    Remove-Item -LiteralPath $resolvedStagingDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}

& $dotnetCommand publish $projectPath `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --property:PublishProfile=win-x64 `
  --output $resolvedStagingDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $resolvedStagingDirectory -PathType Container)) {
    throw "Publish output was not found at $resolvedStagingDirectory"
}

Assert-PublishDirectory -Directory $resolvedStagingDirectory

$publishDirectoryInUse = Get-Process -Name 'SvgLiveEditor' -ErrorAction SilentlyContinue |
  Where-Object {
      $null -ne $_.Path -and
      $_.Path.StartsWith(
          $resolvedPublishDirectory + [IO.Path]::DirectorySeparatorChar,
          [StringComparison]::OrdinalIgnoreCase)
  } |
  Select-Object -First 1

$archiveSource = $resolvedPublishDirectory
if ($null -ne $publishDirectoryInUse) {
    if (Test-Path -LiteralPath $resolvedCandidateDirectory) {
        Remove-Item -LiteralPath $resolvedCandidateDirectory -Recurse -Force
    }

    Move-Item -LiteralPath $resolvedStagingDirectory -Destination $resolvedCandidateDirectory
    $archiveSource = $resolvedCandidateDirectory
    Write-Warning "The current dist\win-x64 is in use. Clean output is available at $resolvedCandidateDirectory."
}
else {
    if (Test-Path -LiteralPath $resolvedPublishDirectory) {
        Remove-Item -LiteralPath $resolvedPublishDirectory -Recurse -Force
    }

    Move-Item -LiteralPath $resolvedStagingDirectory -Destination $resolvedPublishDirectory
}

Assert-PublishDirectory -Directory $archiveSource
Compress-Archive -Path (Join-Path $archiveSource '*') -DestinationPath $archivePath -Force
Assert-ZipPackage -Path $archivePath

$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumLine = "$archiveHash  $archiveFileName`n"
$utf8WithoutBom = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($checksumPath, $checksumLine, $utf8WithoutBom)

Write-Host "Created $archivePath"
Write-Host "Created $checksumPath"

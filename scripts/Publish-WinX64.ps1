[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $repositoryRoot 'dist\win-x64'
$candidateDirectory = Join-Path $repositoryRoot 'dist\win-x64-candidate'
$stagingDirectory = Join-Path $repositoryRoot "dist\.win-x64-staging-$PID"
$releaseDirectory = Join-Path $repositoryRoot 'releases'
$archivePath = Join-Path $releaseDirectory 'SvgLiveEditor-v0.1.0-win-x64.zip'
$localDotnet = Join-Path $repositoryRoot '.dotnet\dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$resolvedRepositoryRoot = [IO.Path]::GetFullPath($repositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$resolvedPublishDirectory = [IO.Path]::GetFullPath($publishDirectory)
$resolvedCandidateDirectory = [IO.Path]::GetFullPath($candidateDirectory)
$resolvedStagingDirectory = [IO.Path]::GetFullPath($stagingDirectory)

foreach ($directory in @(
        $resolvedPublishDirectory,
        $resolvedCandidateDirectory,
        $resolvedStagingDirectory)) {
    if (-not $directory.StartsWith(
            $resolvedRepositoryRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to manage publish output outside the repository: $directory"
    }
}

New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null

if (Test-Path -LiteralPath $resolvedStagingDirectory) {
    Remove-Item -LiteralPath $resolvedStagingDirectory -Recurse -Force
}

& $dotnetCommand publish (Join-Path $repositoryRoot 'src\SvgLiveEditor\SvgLiveEditor.csproj') `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --property:PublishProfile=win-x64 `
  --output $resolvedStagingDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $resolvedStagingDirectory)) {
    throw "Publish output was not found at $resolvedStagingDirectory"
}

$forbiddenRuntimeData = Get-ChildItem -LiteralPath $resolvedStagingDirectory -Recurse -Force |
  Where-Object {
      $_.Name -eq 'EBWebView' -or
      $_.Name.EndsWith('.WebView2', [StringComparison]::OrdinalIgnoreCase)
  } |
  Select-Object -First 1
if ($null -ne $forbiddenRuntimeData) {
    throw "Refusing to package WebView2 user data: $($forbiddenRuntimeData.FullName)"
}

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

Compress-Archive -Path (Join-Path $archiveSource '*') -DestinationPath $archivePath -Force
Write-Host "Created $archivePath"

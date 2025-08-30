[CmdletBinding()]
param(
  [switch]$NoPrerelease,                # --no-prerelease
  [string]$Suffix = "preview",          # --suffix
  [string]$ReleaseVersion = "",         # --release-version (required if -NoPrerelease)
  [string]$Configuration = "Release",   # -c / --configuration
  [string]$Paths = ""                   # --paths (comma-separated roots)
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Resolve-Path (Join-Path $ScriptDir "..")

function Get-BaseVersion {
  (Get-Content -Path (Join-Path $ScriptDir "VERSION") -Raw).Trim()
}

function Compute-Version {
  param([bool]$Prerelease)
  if (-not $Prerelease) {
    if ([string]::IsNullOrWhiteSpace($ReleaseVersion)) {
      Write-Error "--release-version is required when --no-prerelease is set"
    }
    return $ReleaseVersion
  }
  $base = Get-BaseVersion
  $run  = if ($env:GITHUB_RUN_NUMBER) { $env:GITHUB_RUN_NUMBER } else { (Get-Date -Format "yyyyMMddHHmm") }
  $sha  = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { $(git rev-parse --short HEAD 2>$null) }
  if (-not $sha) { $sha = "local" } else { $sha = $sha.Substring(0, [Math]::Min(7, $sha.Length)) }
  return "{0}-{1}.{2}+sha.{3}" -f $base, $Suffix, $run, $sha
}

$isPrerelease = -not $NoPrerelease.IsPresent
$version = Compute-Version -Prerelease:$isPrerelease
Write-Host ("Packing version {0} (Prerelease={1})" -f $version, $isPrerelease)

Push-Location $RepoRoot
try {
  $outDir = Join-Path $RepoRoot "artifacts/nupkg"
  New-Item -ItemType Directory -Path $outDir -Force | Out-Null

  & dotnet --info | Out-Null
  & dotnet restore

  # Collect projects to pack
  $projects = @()
  if (-not [string]::IsNullOrWhiteSpace($Paths)) {
    $roots = $Paths.Split(",") | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    foreach ($p in $roots) {
      $abs = Join-Path $RepoRoot $p
      if (Test-Path $abs) {
        $projects += Get-ChildItem -Path $abs -Filter *.csproj -Recurse | Where-Object { $_.Name -notmatch "Tests\.csproj$" } | ForEach-Object { $_.FullName }
      }
    }
  } else {
    $src = Join-Path $RepoRoot "src"
    if (Test-Path $src) {
      $projects += Get-ChildItem -Path $src -Filter *.csproj -Recurse | Where-Object { $_.Name -notmatch "Tests\.csproj$" } | ForEach-Object { $_.FullName }
    }
  }

  $projects = $projects | Sort-Object -Unique
  if (-not $projects -or $projects.Count -eq 0) {
    Write-Error "No projects found to pack."
  }

  foreach ($proj in $projects) {
    Write-Host "Packing $proj"
    & dotnet pack $proj -c $Configuration -p:Version="$version" -p:ContinuousIntegrationBuild=true --include-symbols -p:SymbolPackageFormat=snupkg -o $outDir
  }

  Write-Host "Packages written to $outDir"
} finally {
  Pop-Location
}

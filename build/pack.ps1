param(
    [switch]$PreRelease = $true,
    [string]$Suffix = "preview",
    [string]$ReleaseVersion,
    [string]$Configuration = "Release",
    [string[]]$Paths
)

$ErrorActionPreference = 'Stop'

function Get-BaseVersion {
    $versionPath = Join-Path $PSScriptRoot 'VERSION'
    if (-not (Test-Path $versionPath)) {
        throw "VERSION file not found at $versionPath"
    }
    (Get-Content $versionPath -Raw).Trim()
}

function Get-Version {
    param(
        [bool]$IsPre,
        [string]$Suffix,
        [string]$ReleaseVersion
    )
    if (-not $IsPre) { return $ReleaseVersion }
    $base = Get-BaseVersion
    $run = if ($env:GITHUB_RUN_NUMBER) { $env:GITHUB_RUN_NUMBER } else { Get-Date -Format 'yyyyMMddHHmm' }
    $sha = if ($env:GITHUB_SHA) { $env:GITHUB_SHA.Substring(0,7) } else { (git rev-parse --short HEAD) 2>$null }
    if ([string]::IsNullOrWhiteSpace($sha)) { $sha = 'local' }
    return "$base-$Suffix.$run+sha.$sha"
}

function Get-Projects {
    param([string[]]$Paths)
    $root = Resolve-Path (Join-Path $PSScriptRoot '..')
    if ($Paths -and $Paths.Length -gt 0) {
        $all = @()
        foreach ($p in $Paths) {
            if (-not [string]::IsNullOrWhiteSpace($p)) {
                $dir = Join-Path $root $p
                if (Test-Path $dir) {
                    $all += Get-ChildItem -Path $dir -Recurse -Filter *.csproj | Where-Object { $_.Name -notmatch 'Tests\.csproj$' }
                }
            }
        }
        return $all | Select-Object -Unique
    }
    else {
        return Get-ChildItem -Path (Join-Path $root 'src') -Recurse -Filter *.csproj | Where-Object { $_.Name -notmatch 'Tests\.csproj$' }
    }
}

$isPre = $PreRelease -and [string]::IsNullOrWhiteSpace($ReleaseVersion)
if (-not $isPre -and [string]::IsNullOrWhiteSpace($ReleaseVersion)) {
    throw "Provide -ReleaseVersion for stable builds or use -PreRelease."
}

$version = Get-Version -IsPre:$isPre -Suffix:$Suffix -ReleaseVersion:$ReleaseVersion
Write-Host "Packing version $version (PreRelease=$isPre)"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    dotnet --info | Out-Null
    dotnet restore
    $outDir = Join-Path $repoRoot 'artifacts' 'nupkg'
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    $projects = Get-Projects -Paths:$Paths
    foreach ($proj in $projects) {
        Write-Host "Packing $($proj.FullName)"
        dotnet pack $proj.FullName -c $Configuration -p:Version=$version -p:ContinuousIntegrationBuild=true --include-symbols -p:SymbolPackageFormat=snupkg -o $outDir
    }

    Write-Host "Packages written to $outDir"
}
finally {
    Pop-Location
}


# SPDX-License-Identifier: BUSL-1.1

<#
.SYNOPSIS
    Creates a Coven shortcut carrying the application icon.

.DESCRIPTION
    Coven.cmd is the launcher that ships with the repository, but a .cmd file always shows
    Explorer's generic script icon — a batch file cannot carry one of its own. This writes a
    shortcut to that launcher with the witch hat attached.

    Shortcuts are not committed and cannot be: a .lnk stores absolute paths, so one is only
    ever valid on the machine that made it. That is the whole reason this script exists rather
    than a checked-in file.

.PARAMETER Desktop
    Also place a copy on the Desktop.

.PARAMETER StartMenu
    Also place a copy in the Start Menu, where Windows Search will find it.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\New-CovenShortcut.ps1 -Desktop
#>

[CmdletBinding()]
param(
    [switch]$Desktop,
    [switch]$StartMenu
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$launcher = Join-Path $root 'Coven.cmd'
$icon = Join-Path $root 'src\apps\Coven.Ui.Desktop\Assets\coven.ico'

if (-not (Test-Path $launcher)) {
    throw "Launcher not found at $launcher. Run this from a clone of the Coven repository."
}
if (-not (Test-Path $icon)) {
    throw "Icon not found at $icon."
}

$targets = [System.Collections.Generic.List[string]]::new()
$targets.Add((Join-Path $root 'Coven.lnk'))
if ($Desktop) {
    $targets.Add((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Coven.lnk'))
}
if ($StartMenu) {
    $programs = [Environment]::GetFolderPath('Programs')
    if (-not (Test-Path $programs)) { New-Item -ItemType Directory -Path $programs | Out-Null }
    $targets.Add((Join-Path $programs 'Coven.lnk'))
}

$shell = New-Object -ComObject WScript.Shell

foreach ($path in $targets) {
    $link = $shell.CreateShortcut($path)
    $link.TargetPath = $launcher
    $link.WorkingDirectory = $root
    # ,0 selects the first icon in the file. The icon is referenced from the source tree
    # rather than the build output so the shortcut looks right before anything is compiled.
    $link.IconLocation = "$icon,0"
    $link.Description = 'Coven desktop chat'
    # Minimised, so the launcher's console does not flash on the way to the window.
    $link.WindowStyle = 7
    $link.Save()

    Write-Host "Created $path"
}

if (-not (Test-Path (Join-Path $root 'src\apps\Coven.Ui.Desktop\bin'))) {
    Write-Host ''
    Write-Host 'Coven has not been built yet. The shortcut will build it on first use,'
    Write-Host 'which takes a few minutes; afterwards it starts immediately.'
}

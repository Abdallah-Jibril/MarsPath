param([switch]$Launch)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$player = Get-Item -LiteralPath (Join-Path $repoRoot 'MarsPathUnity/Builds/Windows/MarsPath.exe')
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
$shortcutPath = Join-Path $desktop 'MarsPath.lnk'

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $player.FullName
$shortcut.WorkingDirectory = $player.DirectoryName
$shortcut.Arguments = '-screen-fullscreen 1 -window-mode borderless'
$shortcut.IconLocation = "$($player.FullName),0"
$shortcut.Description = 'Launch MarsPath in borderless fullscreen'
$shortcut.Save()

Get-Item -LiteralPath $shortcutPath
if ($Launch) { Start-Process -FilePath $shortcutPath }

# Run from the extracted published package. No administrator access required.
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'VREC.exe'
if (!(Test-Path $source)) { throw 'Extract the published VREC package before running Install.ps1.' }
if (Get-Process VREC -ErrorAction SilentlyContinue) { throw 'Exit VREC from its tray menu before installing.' }
$destination = Join-Path $env:LOCALAPPDATA 'Programs/VREC'
New-Item -ItemType Directory -Force $destination | Out-Null
Copy-Item $source (Join-Path $destination 'VREC.exe') -Force
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'VREC.lnk'))
$shortcut.TargetPath = Join-Path $destination 'VREC.exe'
$shortcut.WorkingDirectory = $destination
$shortcut.Save()
Start-Process (Join-Path $destination 'VREC.exe')
Write-Host 'Installed per user. Enable Start with Windows in VREC settings if desired.'

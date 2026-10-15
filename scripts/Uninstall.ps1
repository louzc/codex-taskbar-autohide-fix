[CmdletBinding(SupportsShouldProcess = $true)]
param()
$ErrorActionPreference = 'Stop'
$installDirectory = Join-Path $env:LOCALAPPDATA 'ChatGPTTaskbarFix'
$installedExe = Join-Path $installDirectory 'ChatGPTTaskbarFix.exe'
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'ChatGPT Taskbar Compatibility.lnk'
if (Test-Path -LiteralPath $shortcutPath) {
  $shell = New-Object -ComObject WScript.Shell
  if ($shell.CreateShortcut($shortcutPath).TargetPath -ne $installedExe) { throw 'The startup shortcut points to another program; refusing to remove it.' }
}
if (-not $PSCmdlet.ShouldProcess($installDirectory, 'Remove login startup, stop the helper, restore its window properties and delete its known files')) { return }
if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath }
if (Test-Path -LiteralPath $installedExe) {
  $running = @(Get-Process -Name ChatGPTTaskbarFix -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $installedExe })
  $null = Start-Process -FilePath $installedExe -ArgumentList '--stop' -WindowStyle Hidden -Wait -PassThru
  foreach ($process in $running) {
    if (-not $process.WaitForExit(8000)) { throw 'The helper did not stop; program files have not been deleted.' }
  }
}
foreach ($name in @('ChatGPTTaskbarFix.exe','ChatGPTTaskbarFix.cs','installation.json','status.json','events.log')) {
  $path = Join-Path $installDirectory $name
  if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
}
if ((Test-Path -LiteralPath $installDirectory) -and @(Get-ChildItem -LiteralPath $installDirectory -Force).Count -eq 0) { Remove-Item -LiteralPath $installDirectory }
Write-Output 'Removed login startup and helper files. Graceful shutdown restores the previous window properties. Taskbar auto-hide settings were not changed.'

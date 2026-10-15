[CmdletBinding(SupportsShouldProcess = $true)]
param()
$ErrorActionPreference = 'Stop'
function Get-ExecutableHash([string]$Path) {
  $stream = [IO.File]::OpenRead($Path)
  $algorithm = [Security.Cryptography.SHA256]::Create()
  try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','') }
  finally { $stream.Dispose(); $algorithm.Dispose() }
}
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceExe = Join-Path $repoRoot 'bin\ChatGPTTaskbarFix.exe'
$manifest = Join-Path $repoRoot 'bin\SHA256SUMS.txt'
$line = (Get-Content -LiteralPath $manifest | Where-Object { $_ -match '^([a-fA-F0-9]{64})  ChatGPTTaskbarFix\.exe$' } | Select-Object -First 1)
if (-not $line) { throw 'Executable checksum not found in bin/SHA256SUMS.txt.' }
$expectedHash = $line.Substring(0,64)
$sourceHash = Get-ExecutableHash $sourceExe
if ($sourceHash -ne $expectedHash) { throw 'The executable does not match its checksum.' }
$installDirectory = Join-Path $env:LOCALAPPDATA 'ChatGPTTaskbarFix'
$installedExe = Join-Path $installDirectory 'ChatGPTTaskbarFix.exe'
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'ChatGPT Taskbar Compatibility.lnk'
$shell = New-Object -ComObject WScript.Shell
if (Test-Path -LiteralPath $shortcutPath) {
  if ($shell.CreateShortcut($shortcutPath).TargetPath -ne $installedExe) { throw 'An unrelated startup shortcut already uses this name.' }
}
if (-not $PSCmdlet.ShouldProcess($installDirectory, 'Install the helper for the current user, configure login startup and run it')) { return }
if (Test-Path -LiteralPath $installedExe) {
  if ((Get-ExecutableHash $installedExe) -ne $expectedHash) {
    $running = @(Get-Process -Name ChatGPTTaskbarFix -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $installedExe })
    if ($running.Count) {
      $null = Start-Process -FilePath $installedExe -ArgumentList '--stop' -WindowStyle Hidden -Wait -PassThru
      foreach ($process in $running) { if (-not $process.WaitForExit(8000)) { throw 'The existing helper did not stop; no executable was overwritten.' } }
    }
  }
}
$null = New-Item -ItemType Directory -Path $installDirectory -Force
if (-not (Test-Path -LiteralPath $installedExe) -or (Get-ExecutableHash $installedExe) -ne $expectedHash) {
  Copy-Item -LiteralPath $sourceExe -Destination $installedExe -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\ChatGPTTaskbarFix.cs') -Destination (Join-Path $installDirectory 'ChatGPTTaskbarFix.cs') -Force
if ((Get-ExecutableHash $installedExe) -ne $expectedHash) { throw 'Installed executable checksum verification failed.' }
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $installedExe
$shortcut.WorkingDirectory = $installDirectory
$shortcut.WindowStyle = 7
$shortcut.Description = 'Taskbar auto-hide compatibility for maximized OpenAI Codex windows.'
$shortcut.Save()
if ($shell.CreateShortcut($shortcutPath).TargetPath -ne $installedExe) { throw 'Startup shortcut verification failed.' }
$null = Start-Process -FilePath $installedExe -WindowStyle Hidden -PassThru
Start-Sleep -Milliseconds 1500
$running = @(Get-Process -Name ChatGPTTaskbarFix -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $installedExe })
if ($running.Count -ne 1) { throw 'Expected one running helper. Check events.log in the installation directory.' }
[ordered]@{InstalledAt=(Get-Date).ToString('o');Binary=$installedExe;SHA256=$expectedHash.ToLowerInvariant();StartupShortcut=$shortcutPath;Version='1.0.0'} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installDirectory 'installation.json') -Encoding UTF8
Write-Output "Installed and running: $installedExe"
Write-Output "Login startup: $shortcutPath"

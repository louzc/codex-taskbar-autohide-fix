[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'bin' }
$source = Join-Path $repoRoot 'src\ChatGPTTaskbarFix.cs'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler was not found.' }
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$binary = Join-Path $OutputDirectory 'ChatGPTTaskbarFix.exe'
& $compiler /nologo /optimize+ /target:winexe /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "/out:$binary" $source
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
$hash = (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'), "$hash  ChatGPTTaskbarFix.exe" + [char]10, [Text.Encoding]::ASCII)
Write-Output "Built: $binary"
Write-Output "SHA256: $hash"

# .claude/verify.ps1 — das EINE Pruefkommando dieses Projekts (Verify-Konvention, globale CLAUDE.md).
#   exit 0 = Build UND Tests gruen; exit != 0 = kaputt, Fehler in der Ausgabe.
# Uebernommen 2026-10-12 aus dem frueheren globalen Dalamud-Stop-Hook, der genau das ausgefuehrt hat.
#
# verify-on-stop: off
#   Stand 2026-10-12 ist der Build schon vorher ROT: das Submodul KamiToolKit passt nicht zur
#   aktuellen FFXIVClientStructs-Version (37 Fehler, u. a. CS0305 ICreatable<T>). Submodule werden
#   nicht editiert -> erst Submodul aktualisieren, dann diese Zeile entfernen.

$ErrorActionPreference = 'Continue'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root
$dotnet = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }
& $dotnet build Echosync.sln -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) { Write-Error "verify.ps1: Build fehlgeschlagen"; exit 1 }
& $dotnet test Echosync.sln --no-build -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) { Write-Error "verify.ps1: Tests fehlgeschlagen"; exit 1 }
exit 0

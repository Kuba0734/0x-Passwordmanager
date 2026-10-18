# Release von Password Vault (Windows) über GitHub Releases.
# Ablauf: Werkzeuge prüfen → Version wählen → bauen → ZIP + SHA-256 → Setup → Commit/Tag/Push → Release.
# Aufruf: deploy.bat (oder: powershell -ExecutionPolicy Bypass -File deploy.ps1)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# Muss zur App passen (PasswordVault.App\Helpers\UpdateService.cs: Repo, ZipName)
$Repo      = 'Kuba0734/0x-Passwordmanager'
$App       = 'PasswordVault'
$Project   = 'PasswordVault.App\PasswordVault.App.csproj'
$Iss       = 'installer\PasswordVault.iss'
$ExeName   = 'PasswordVault.App.exe'
$ZipName   = "$App-win-x64.zip"
$SetupName = "${App}Setup-x64.exe"
$Artifacts = Join-Path $PSScriptRoot 'artifacts'
$Publish   = Join-Path $Artifacts 'publish'

function Fail([string]$Message) {
    Write-Host ''
    Write-Host "[FEHLER] $Message" -ForegroundColor Red
    exit 1
}

function Step([string]$Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# ---------------------------------------------------------------- Werkzeuge
Step 'Prüfe Werkzeuge'
$missing = @()
if (-not (Get-Command git -ErrorAction SilentlyContinue))    { $missing += 'Git:          winget install --id Git.Git -e' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $missing += '.NET SDK:     winget install --id Microsoft.DotNet.SDK.8 -e' }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    $missing += 'GitHub CLI:   winget install --id GitHub.cli -e   (danach: gh auth login)'
} else {
    cmd /c "gh auth status >nul 2>&1"
    if ($LASTEXITCODE -ne 0) { $missing += 'GitHub CLI ist nicht angemeldet:   gh auth login' }
}
$Iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $Iscc) { $missing += 'Inno Setup:   winget install --id JRSoftware.InnoSetup -e' }

if ($missing.Count -gt 0) {
    Write-Host 'Es fehlt noch etwas:' -ForegroundColor Yellow
    $missing | ForEach-Object { Write-Host "  $_" }
    Fail 'Bitte installieren und deploy.bat erneut starten.'
}
Write-Host '  git, dotnet, gh (angemeldet) und Inno Setup gefunden.'

# ---------------------------------------------------------------- Version
Step 'Version'
cmd /c "git fetch --tags --quiet >nul 2>&1"
$versions = git tag --list 'v*' | ForEach-Object {
    if ($_ -match '^v(\d+)\.(\d+)\.(\d+)$') { [version]"$($Matches[1]).$($Matches[2]).$($Matches[3])" }
} | Sort-Object
$last = $versions | Select-Object -Last 1
$suggest = if ($last) { '{0}.{1}.{2}' -f $last.Major, $last.Minor, ($last.Build + 1) } else { '1.0.0' }
Write-Host ("  Letztes Release: " + $(if ($last) { "v$last" } else { '(noch keins)' }))

$Version = (Read-Host "  Neue Version [$suggest]").Trim().TrimStart('v', 'V')
if (-not $Version) { $Version = $suggest }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { Fail "'$Version' ist keine Version im Format X.Y.Z." }
if ($last -and [version]$Version -le $last) { Fail "v$Version ist nicht neuer als v$last." }
if (git tag --list "v$Version") { Fail "Der Tag v$Version existiert bereits." }

$Notes = ''
while (-not $Notes.Trim()) { $Notes = Read-Host '  Was hat sich geändert?' }
$Notes = $Notes.Trim()
# Texte über Dateien übergeben – PowerShell 5.1 zerlegt Argumente mit Anführungszeichen falsch
$Utf8 = New-Object System.Text.UTF8Encoding($false)
$NotesFile = Join-Path $env:TEMP "pv-release-notes.txt"

# ---------------------------------------------------------------- Build (zuerst – ein kaputter Build wird nie veröffentlicht)
Step "Baue Version $Version (Release, win-x64, eigenständig, ohne MSIX)"
if (Test-Path $Artifacts) { Remove-Item $Artifacts -Recurse -Force }
& dotnet publish $Project -c Release -r win-x64 --self-contained `
    -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true `
    -p:Version=$Version -o $Publish
if ($LASTEXITCODE -ne 0) { Fail 'Der Build ist fehlgeschlagen – nichts wurde veröffentlicht.' }
if (-not (Test-Path (Join-Path $Publish $ExeName))) { Fail "$ExeName fehlt im Build." }

# ---------------------------------------------------------------- ZIP + Prüfsumme
Step "Packe $ZipName"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Zip = Join-Path $Artifacts $ZipName
[System.IO.Compression.ZipFile]::CreateFromDirectory($Publish, $Zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$Hash = (Get-FileHash $Zip -Algorithm SHA256).Hash.ToLowerInvariant()
$Sha = "$Zip.sha256"
[System.IO.File]::WriteAllText($Sha, "$Hash  $ZipName`n", $Utf8)
Write-Host ("  {0:N1} MB, SHA-256 {1}" -f ((Get-Item $Zip).Length / 1MB), $Hash)

# ---------------------------------------------------------------- Setup
Step "Baue $SetupName"
& $Iscc /Q "/DAppVersion=$Version" "/DSourceDir=$Publish" "/DOutputDir=$Artifacts" $Iss
if ($LASTEXITCODE -ne 0) { Fail 'Inno Setup ist fehlgeschlagen – nichts wurde veröffentlicht.' }
$Setup = Join-Path $Artifacts $SetupName
if (-not (Test-Path $Setup)) { Fail "$SetupName wurde nicht erzeugt." }

# ---------------------------------------------------------------- Git
Step 'Commit, Tag und Push'
$Branch = (git rev-parse --abbrev-ref HEAD).Trim()
git add -A
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    [System.IO.File]::WriteAllText($NotesFile, "Release v$Version`n`n$Notes`n", $Utf8)
    git commit -F $NotesFile
    if ($LASTEXITCODE -ne 0) { Fail 'Commit fehlgeschlagen.' }
} else {
    Write-Host '  Keine Dateiänderungen – nur Tag.'
}
[System.IO.File]::WriteAllText($NotesFile, "$Notes`n", $Utf8)
git tag -a "v$Version" -F $NotesFile
if ($LASTEXITCODE -ne 0) { Fail 'Tag konnte nicht angelegt werden.' }
git push origin $Branch
if ($LASTEXITCODE -ne 0) { Fail "Push von $Branch fehlgeschlagen (evtl. vorher 'git pull')." }
git push origin "v$Version"
if ($LASTEXITCODE -ne 0) { Fail "Push des Tags v$Version fehlgeschlagen." }

# ---------------------------------------------------------------- GitHub Release
Step 'GitHub-Release'
$ReleaseNotes = @"
$Notes

### Installation
**$SetupName** herunterladen und ausführen – keine Admin-Rechte nötig.
Bereits installierte Versionen aktualisieren sich automatisch.

<sub>$ZipName und die .sha256-Datei werden vom eingebauten Updater verwendet.</sub>
"@
[System.IO.File]::WriteAllText($NotesFile, $ReleaseNotes, $Utf8)
& gh release create "v$Version" $Setup $Zip $Sha --repo $Repo --title "Password Vault $Version" --notes-file $NotesFile --latest
if ($LASTEXITCODE -ne 0) {
    Fail "Release fehlgeschlagen. Tag v$Version ist bereits gepusht – Release von Hand nachholen:`n  gh release create v$Version `"$Setup`" `"$Zip`" `"$Sha`" --repo $Repo --latest"
}

Write-Host ''
Write-Host "Fertig: Password Vault $Version ist veröffentlicht." -ForegroundColor Green
Write-Host "  https://github.com/$Repo/releases/tag/v$Version"

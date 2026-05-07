# ─────────────────────────────────────────────────────────────────────────────
#  Trakr — Velopack Build & Release Script
#
#  PREREQUISITES
#  ─────────────
#  1. Install the vpk CLI:
#       dotnet tool install -g vpk
#
#  2. Set your GitHub token so vpk can upload releases:
#       $env:GITHUB_TOKEN = "ghp_xxxxxxxxxxxxxxxxxxxx"
#
#  3. Edit REPO_URL below to point to your actual GitHub repository.
#
#  USAGE
#  ─────
#  Build only (no upload):
#       .\build.ps1
#
#  Build + publish to GitHub Releases:
#       .\build.ps1 -Publish
#
# ─────────────────────────────────────────────────────────────────────────────

param(
    [switch]$Publish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Configuration ──────────────────────────────────────────────────────────────
$APP_ID      = "Trakr"
$VERSION     = "1.0.0"           # bump this with each release
$REPO_URL    = "https://github.com/OWNER/Trakr"   # <─ change this!
$PUBLISH_DIR = ".\publish"
$RELEASES_DIR = ".\Releases"

# ── 1. Build framework-dependent publish ───────────────────────────────────────
Write-Host "`n[1/3] Publishing .NET app…" -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 --no-self-contained -o $PUBLISH_DIR
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# ── 2. Pack with Velopack ──────────────────────────────────────────────────────
Write-Host "`n[2/3] Packing with vpk…" -ForegroundColor Cyan
if (!(Test-Path $RELEASES_DIR)) { New-Item -ItemType Directory $RELEASES_DIR | Out-Null }

# -f net8-x64-desktop  →  bundles .NET 8 Desktop Runtime prerequisite check
vpk pack `
    --packId       $APP_ID `
    --packVersion  $VERSION `
    --packDir      $PUBLISH_DIR `
    --outputDir    $RELEASES_DIR `
    --mainExe      "Trakr.exe" `
    --framework    "net8-x64-desktop"

if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

Write-Host "`nRelease files written to: $RELEASES_DIR" -ForegroundColor Green
Get-ChildItem $RELEASES_DIR | Format-Table Name, Length -AutoSize

# ── 3. (Optional) Upload to GitHub Releases ────────────────────────────────────
if ($Publish) {
    if (-not $env:GITHUB_TOKEN) {
        throw "Set `$env:GITHUB_TOKEN before publishing (e.g. `$env:GITHUB_TOKEN = 'ghp_...'`)"
    }

    Write-Host "`n[3/3] Uploading to GitHub Releases…" -ForegroundColor Cyan
    vpk upload github `
        --repoUrl  $REPO_URL `
        --outputDir $RELEASES_DIR `
        --publish `
        --tag      "v$VERSION" `
        --token    $env:GITHUB_TOKEN

    if ($LASTEXITCODE -ne 0) { throw "vpk upload failed" }
    Write-Host "`nPublished v$VERSION to $REPO_URL/releases" -ForegroundColor Green
}
else {
    Write-Host "`nSkipping GitHub upload. Run with -Publish to upload." -ForegroundColor Yellow
}

# Uploads the mod installed in Mods to its Steam Workshop item, and to mod.io
# when there's a token and a zip, with the newest changelog entry as the change
# note. The Windows version of upload.sh:
#
#   examples\upload.ps1 [-DryRun]
#
# Starts Timberborn through Steam if it isn't running, waits for the result at
# the main menu and closes the game again if it was the one to start it. Steam
# has to be running and signed in.
param([switch]$DryRun)
$ErrorActionPreference = "Stop"

# ---- This repo ---------------------------------------------------------------
$id = "MyMod"
$modioGame = 0
$modioMod = 0
$changelog = "changelog.txt"
# -----------------------------------------------------------------------------

$timberborn = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "Timberborn"
$flag = Join-Path $timberborn "ModUploader-request.json"
$result = "$flag.result"
$token = Join-Path $env:USERPROFILE ".config\timberborn-mod-uploader\modio-token"

# The newest entry: from its v<version> line to the first empty line
$lines = Get-Content $changelog
$version = $lines[0] -replace "^v", ""
$note = ($lines | Select-Object -Skip 1 | Where-Object { $_ -ne "" } | Select-Object -First ([array]::IndexOf(($lines | Select-Object -Skip 1), "")) ) -join "`n"
if (-not $note) { $note = ($lines | Select-Object -Skip 1) -join "`n" }
$manifest = Get-ChildItem (Join-Path $timberborn "Mods\$id") -Recurse -Filter manifest.json | Sort-Object FullName | Select-Object -Last 1
$have = (Get-Content $manifest.FullName -Raw | ConvertFrom-Json).Version
if ($have -ne $version) { throw "Mods has $id $have, the changelog is at ${version}: release first" }

Write-Host "uploading $id $version with:`n$note"
if ($DryRun) { Write-Host "(dry run: nothing uploaded)"; exit 0 }

Remove-Item $result -ErrorAction SilentlyContinue
$request = @{ mod = $id; changelog = $note }
$zip = Get-ChildItem (Join-Path $HOME "Downloads") -Filter "$id-*.zip" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
if ($zip -and (Test-Path $token)) {
    $request.modio = @{ game = $modioGame; mod = $modioMod; zip = $zip.FullName; version = $version }
}
$request | ConvertTo-Json -Depth 4 | Set-Content $flag -Encoding UTF8

$started = $false
try {
    if (-not (Get-Process Timberborn -ErrorAction SilentlyContinue)) {
        if (-not (Get-Process steam -ErrorAction SilentlyContinue)) { throw "Steam isn't running" }
        Write-Host "starting Timberborn..."
        Start-Process "steam://rungameid/1062090"
        $started = $true
    }
    $waited = 0
    while (-not (Test-Path $result)) {
        if ($waited -ge 300) { throw "no answer from Timberborn after 5 minutes: is Mod Uploader enabled in it?" }
        Start-Sleep 1
        $waited++
    }
    $failed = $false
    foreach ($line in Get-Content $result) {
        $target, $status, $rest = $line -split " ", 3
        if ("$target $status" -eq "steam ok") { Write-Host "uploaded: https://steamcommunity.com/sharedfiles/filedetails/?id=$rest" }
        elseif ("$target $status" -eq "modio ok") { Write-Host "uploaded to mod.io: file $rest" }
        elseif ($line) { Write-Error $line -ErrorAction Continue; $failed = $true }
    }
    Remove-Item $result
    if ($failed) { exit 1 }
}
finally {
    Remove-Item $flag -ErrorAction SilentlyContinue
    if ($started) { Stop-Process -Name Timberborn -ErrorAction SilentlyContinue }
}

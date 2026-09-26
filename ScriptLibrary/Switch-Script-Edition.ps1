param(
    [ValidateSet('Basic', 'Advanced')][string]$Edition,
    [ValidateSet('01_Movement', '02_Tool', '03_CellWorld', '04_Plants')][string]$Module
)

# This utility belongs BESIDE Assets. It never runs inside the game.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (!(Test-Path -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'Keep ScriptLibrary directly inside your Unity project folder.'
}
if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw 'Save your scenes and close Unity Editor before switching scripts.'
}

$catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'modules.json') -Raw | ConvertFrom-Json
if (!$Edition) {
    Write-Host '1 = Basic scripts; 2 = Advanced scripts'
    $choice = Read-Host 'Choose edition'
    if ($choice -eq '1') { $Edition = 'Basic' }
    elseif ($choice -eq '2') { $Edition = 'Advanced' }
    else { throw 'Choose 1 or 2.' }
}
if (!$Module) {
    Write-Host '1 = Movement; 2 = Water/flame tool; 3 = CellWorld; 4 = Plants'
    $choice = Read-Host 'Choose one module'
    $names = @('01_Movement', '02_Tool', '03_CellWorld', '04_Plants')
    if ($choice -notin @('1','2','3','4')) { throw 'Choose 1, 2, 3 or 4.' }
    $Module = $names[[int]$choice - 1]
}
if ($Module -eq '01_Movement') {
    throw 'Movement switching is retired for this ladder-free version.'
}
if ($Module -eq '02_Tool' -and (Test-Path -LiteralPath (Join-Path $projectRoot 'Assets/Game/Scripts/Weapons/PressureSprayVisuals.cs'))) {
    throw 'Archived tool editions do not include the current spray helpers. Restore a complete project version through Git instead.'
}
$entry = $catalog.$Module
$sourceRoot = Join-Path $PSScriptRoot "$Edition/$Module/Files"
$sourceFiles = @($entry.$Edition)
$ownedPaths = @(@($entry.Basic.path) + @($entry.Advanced.path) | Sort-Object -Unique)

function Get-ProjectTarget([string]$relative) {
    $path = [IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
    $allowedRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Assets/Game/Scripts')) + [IO.Path]::DirectorySeparatorChar
    if (!$path.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing a path outside game scripts: $relative"
    }
    return $path
}

# Built-in .NET hashing also works when Windows PowerShell cannot auto-load Get-FileHash.
function Get-SavedHash([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $algorithm.Dispose() }
}

# Validate the complete package before changing anything.
foreach ($relative in $ownedPaths) { $null = Get-ProjectTarget $relative }
foreach ($file in $sourceFiles) {
    $source = Join-Path $sourceRoot $file.path
    if (!(Test-Path -LiteralPath $source) -or (Get-SavedHash $source) -ne $file.sha256) {
        throw "Package file is missing or changed: $source. Edit the active Assets copy, not this reference package."
    }
}

# Preserve your edited scripts before replacing or removing any of them.
$projectName = Split-Path $projectRoot -Leaf
$backupBase = Join-Path (Split-Path $projectRoot -Parent) ($projectName + '_Feature_Library/Script_Switch_Backups')
$backup = Join-Path $backupBase ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$oldFiles = @()
foreach ($relative in $ownedPaths) {
    $target = Get-ProjectTarget $relative
    $oldHash = $null
    if (Test-Path -LiteralPath $target) {
        $saved = Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path $saved -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $saved
        $oldHash = (Get-SavedHash $target)
        if ((Get-SavedHash $saved) -ne $oldHash) { throw 'Backup verification failed.' }
    }
    $oldFiles += @{path=$relative; sha256=$oldHash}
}
@{edition=$Edition; module=$Module; project=$projectRoot; before=$oldFiles} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $backup 'switch.json')

foreach ($file in $sourceFiles) {
    $target = Get-ProjectTarget $file.path
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceRoot $file.path) -Destination $target -Force
}
foreach ($relative in $ownedPaths) {
    if ($relative -notin @($sourceFiles.path)) {
        $target = Get-ProjectTarget $relative
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    }
}
foreach ($file in $sourceFiles) {
    if ((Get-SavedHash (Get-ProjectTarget $file.path)) -ne $file.sha256) {
        throw "Copy verification failed. Your previous files are in $backup"
    }
}
Write-Host "Installed $Edition / $Module. Reopen Unity, test, then commit this one change."
Write-Host "Previous scripts saved to: $backup"
Write-Host 'Saved scenes, prefabs and artwork were not replaced. Newly restored advanced settings may use their defaults.'

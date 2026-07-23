param(
    [string]$UnityPath = "C:\Program Files\Unity\Editor\Unity.exe",
    [string]$OutputDirectory = "$PSScriptRoot\..\dist\MineSupport"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$temporaryBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporaryRoot = [System.IO.Path]::GetFullPath((Join-Path $temporaryBase "sdez165-mine-visual-bundle"))
$projectPath = Join-Path $temporaryRoot "Project"
$assetsPath = Join-Path $projectPath "Assets"

function Invoke-Unity {
    param([string[]]$Arguments)

    $process = Start-Process -FilePath $UnityPath -ArgumentList $Arguments -PassThru -NoNewWindow
    for ($i = 0; $i -lt 240; $i++) {
        if ($process.HasExited) {
            return $process.ExitCode
        }

        Start-Sleep -Milliseconds 500
    }

    try {
        $process.Kill()
    }
    catch {
    }
    throw "Unity did not exit within 120 seconds"
}

if (-not $temporaryRoot.StartsWith($temporaryBase, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Temporary project path escaped the system temp directory: $temporaryRoot"
}

if (Test-Path -LiteralPath $temporaryRoot) {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
}

New-Item -ItemType Directory -Path (Join-Path $assetsPath "Editor") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $projectPath "ProjectSettings") -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $repositoryRoot "assets\MineVisuals\MineSpriteEffect.shader") -Destination $assetsPath
Copy-Item -LiteralPath (Join-Path $repositoryRoot "assets\MineVisuals\Editor\MineSupportBundleBuilder.cs") -Destination (Join-Path $assetsPath "Editor")
Set-Content -LiteralPath (Join-Path $projectPath "ProjectSettings\ProjectVersion.txt") -Value "m_EditorVersion: 2018.4.7f1" -Encoding UTF8

$env:MINE_BUNDLE_OUTPUT = [System.IO.Path]::GetFullPath($OutputDirectory)
try {
    $importExitCode = Invoke-Unity @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath",
        $projectPath,
        "-logFile",
        (Join-Path $temporaryRoot "unity-import.log")
    )
    Start-Sleep -Milliseconds 1000

    $staleBundle = Join-Path $OutputDirectory "minevisuals"
    $staleManifest = Join-Path $OutputDirectory "minevisuals.manifest"
    if (Test-Path -LiteralPath $staleBundle) {
        Remove-Item -LiteralPath $staleBundle -Force
    }
    if (Test-Path -LiteralPath $staleManifest) {
        Remove-Item -LiteralPath $staleManifest -Force
    }

    $buildExitCode = Invoke-Unity @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath",
        $projectPath,
        "-executeMethod",
        "MineSupportBundleBuilder.Build",
        "-logFile",
        (Join-Path $temporaryRoot "unity-build.log")
    )
    $buildLog = Join-Path $temporaryRoot "unity-build.log"
    $validated = (Test-Path -LiteralPath $staleBundle) -and
        (Select-String -LiteralPath $buildLog -SimpleMatch "MineSupport bundle built and validated" -Quiet)
    if (($null -ne $buildExitCode -and $buildExitCode -ne 0) -or -not $validated) {
        Get-Content -LiteralPath $buildLog -Encoding UTF8
        throw "Unity AssetBundle build failed with exit code $buildExitCode"
    }
}
finally {
    Remove-Item Env:MINE_BUNDLE_OUTPUT -ErrorAction SilentlyContinue
}

$bundlePath = Join-Path $OutputDirectory "minevisuals"
if (-not (Test-Path -LiteralPath $bundlePath)) {
    throw "Expected AssetBundle was not generated: $bundlePath"
}

Write-Output $bundlePath

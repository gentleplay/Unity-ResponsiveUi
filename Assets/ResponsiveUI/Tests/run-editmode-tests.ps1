# Batch EditMode runner for every Responsive UI test assembly (layout, Fold & Flip, iPhone Duo).
# Skip (exit 0) when the Unity Editor is not found or UNITY_SKIP_EDITMODE_TESTS=1.
# Requires the Editor closed for this project (batchmode cannot share an open project).
#
# Usage (from repo root, Windows PowerShell or pwsh on macOS):
#   pwsh -NoProfile -File ./Assets/ResponsiveUI/Tests/run-editmode-tests.ps1

$ErrorActionPreference = "Stop"
$project = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
# Not Temp/: Unity clears the project's Temp folder on exit, deleting the results
# file after a successful run and making this script report a false failure.
$outDir = Join-Path $project "Logs"
$log = Join-Path $outDir "responsiveui-editmode.log"
$results = Join-Path $outDir "responsiveui-editmode-results.xml"

if ($env:UNITY_SKIP_EDITMODE_TESTS -eq "1") {
    Write-Host "SKIP  UNITY_SKIP_EDITMODE_TESTS=1" -ForegroundColor Yellow
    exit 0
}

# The project's editor version (ProjectSettings/ProjectVersion.txt) under the default Hub folders.
$version = (Get-Content (Join-Path $project "ProjectSettings/ProjectVersion.txt") |
    Where-Object { $_ -like "m_EditorVersion:*" } | Select-Object -First 1) -replace "m_EditorVersion:\s*", ""
$unityCandidates = @(
    $env:UNITY_EDITOR,
    "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
    "/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity"
) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }

if (-not $unityCandidates) {
    Write-Host "SKIP  Unity $version not found. Set UNITY_EDITOR to the Unity executable to run EditMode tests." -ForegroundColor Yellow
    exit 0
}

$unity = @($unityCandidates)[0]
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $results -Force -ErrorAction SilentlyContinue

Write-Host "Project: $project"
Write-Host "Unity:   $unity"
Write-Host "Log:     $log"

$arguments = @(
    "-batchmode",
    "-nographics",
    "-projectPath", "`"$($project.Path)`"",
    "-runTests",
    "-testPlatform", "EditMode",
    "-assemblyNames", "com.futuredigitalplus.responsiveui.tests.editor;com.futuredigitalplus.foldandflip.tests.editor;com.futuredigitalplus.iphoneduo.tests.editor",
    "-testResults", "`"$results`"",
    "-logFile", "`"$log`""
)
$process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru
$code = $process.ExitCode
Write-Host "Unity exit: $code"

$fatalLog =
    (Test-Path -LiteralPath $log) -and
    (Select-String -LiteralPath $log -Pattern `
        "Aborting batchmode due to fatal error|another Unity instance is running|Compilation failed|Scripts have compiler errors" `
        -Quiet)
$hasResults = Test-Path -LiteralPath $results

if ($code -ne 0 -or $fatalLog -or -not $hasResults) {
    if (Test-Path $log) {
        Write-Host "--- log tail ---"
        Get-Content $log -Tail 40
    }
    if ($hasResults) {
        Write-Host "Results: $results"
    } else {
        Write-Host "FAIL  Unity did not produce EditMode test results." -ForegroundColor Red
    }
    if ($code -eq 0) {
        exit 1
    }
    exit $code
}

Write-Host "EditMode tests passed."
exit 0

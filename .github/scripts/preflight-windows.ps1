# Preflight for the release workflow's Windows GPU job (.github/BOOT.md, "Self-hosted runners"): asserts what the
# runner must provide before any build step runs, and names every missing item instead of letting a later step fail
# on an unhelpful error (the 2026-09-19 post-mortem, "Rehearsal before the tag"). Runs with Windows PowerShell 5.1:
# no $IsWindows, no ternary operator. Working directory is the repository root (the job's default), so relative
# paths resolve without $PSScriptRoot.

$failures = New-Object System.Collections.Generic.List[string]

# git
$gitCommand = Get-Command git -ErrorAction SilentlyContinue
if ($gitCommand) {
    Write-Host "git: found ($((git --version)))"
}
else {
    $failures.Add("git not found on PATH")
}

# dotnet SDK named by global.json
$globalJsonPath = "global.json"
if (-not (Test-Path $globalJsonPath)) {
    $failures.Add("global.json not found at the repository root")
}
else {
    $expectedSdk = (Get-Content $globalJsonPath -Raw | ConvertFrom-Json).sdk.version
    $sdks = & dotnet --list-sdks 2>&1
    if ($LASTEXITCODE -ne 0) {
        $failures.Add("dotnet --list-sdks failed: $sdks")
    }
    elseif (-not ($sdks | Select-String -SimpleMatch $expectedSdk)) {
        $failures.Add("dotnet SDK $expectedSdk (global.json) not among the installed SDKs: $($sdks -join '; ')")
    }
    else {
        Write-Host "dotnet SDK ${expectedSdk}: found"
    }
}

# NVIDIA driver
$nvidiaSmi = & nvidia-smi 2>&1
if ($LASTEXITCODE -ne 0) {
    $failures.Add("nvidia-smi did not run (exit code $LASTEXITCODE): $nvidiaSmi")
}
else {
    Write-Host "nvidia-smi: runs"
}

# APTHERMO_NO_CUDA must be unset, or the CUDA tests only check the refusal
if ($env:APTHERMO_NO_CUDA) {
    $failures.Add("APTHERMO_NO_CUDA is set ('$($env:APTHERMO_NO_CUDA)'); the CUDA tests would refuse the accelerator instead of running on it")
}
else {
    Write-Host "APTHERMO_NO_CUDA: unset"
}

if ($failures.Count -gt 0) {
    Write-Host "::error::preflight failed:"
    foreach ($f in $failures) {
        Write-Host "::error::- $f"
    }
    exit 1
}

Write-Host "preflight: all checks passed"

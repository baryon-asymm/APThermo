# Preflight for the release workflow's Windows GPU job (root BOOT.md, "Self-hosted runners"): asserts what the
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

# libnvvm and libdevice.10.bc, via CUDA_PATH or the toolkit directories (root BOOT.md's Dependencies section;
# src/Execution/API.md's Side effects: CUDA_PATH and ProgramFiles are the Windows discovery variables).
$toolkitDirs = New-Object System.Collections.Generic.List[string]
if ($env:CUDA_PATH) {
    $toolkitDirs.Add($env:CUDA_PATH)
}
if ($env:ProgramFiles) {
    $toolkitRoot = Join-Path $env:ProgramFiles "NVIDIA GPU Computing Toolkit\CUDA"
    if (Test-Path $toolkitRoot) {
        Get-ChildItem $toolkitRoot -Directory | Sort-Object Name -Descending | ForEach-Object { $toolkitDirs.Add($_.FullName) }
    }
}

$nvvmFound = $null
$libdeviceFound = $null
foreach ($dir in $toolkitDirs) {
    if (-not $nvvmFound) {
        $candidate = Join-Path $dir "nvvm\bin\x64\nvvm64_40_0.dll"
        if (Test-Path $candidate) {
            $nvvmFound = $candidate
        }
    }
    if (-not $libdeviceFound) {
        $nvvmDir = Join-Path $dir "nvvm"
        if (Test-Path $nvvmDir) {
            $match = Get-ChildItem -Path $nvvmDir -Filter "libdevice.10.bc" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($match) {
                $libdeviceFound = $match.FullName
            }
        }
    }
}

if ($nvvmFound) {
    Write-Host "libnvvm: found at $nvvmFound"
}
else {
    $failures.Add("nvvm64_40_0.dll not found under CUDA_PATH or Program Files\NVIDIA GPU Computing Toolkit\CUDA\v* (nvvm\bin\x64); toolkit directories searched: $($toolkitDirs -join '; ')")
}

if ($libdeviceFound) {
    Write-Host "libdevice.10.bc: found at $libdeviceFound"
}
else {
    $failures.Add("libdevice.10.bc not found under the CUDA toolkit directories searched: $($toolkitDirs -join '; ')")
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

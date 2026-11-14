# Windows PowerShell 5.1 host preflight; see this node's BOOT.md and API.md.
# Command discovery precedes invocation so a missing command cannot inherit a stale exit code.
$failures = New-Object System.Collections.Generic.List[string]

function Invoke-RequiredCommand {
    param([string]$Name, [string[]]$CommandArguments, [System.Collections.Generic.List[string]]$Failures)
    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $command) {
        $Failures.Add("$Name not found on PATH")
        return $null
    }
    try {
        $output = & $command.Source @CommandArguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    catch {
        $Failures.Add("${Name} failed to start: $_")
        return $null
    }
    if ($exitCode -ne 0) {
        $Failures.Add("${Name} failed (exit code ${exitCode}): $($output -join '; ')")
        return $null
    }
    return [PSCustomObject]@{ Output = @($output) }
}

$gitProbe = Invoke-RequiredCommand 'git' @('--version') $failures
if ($gitProbe) {
    Write-Host "git: found ($($gitProbe.Output -join '; '))"
}

$sdkProbe = Invoke-RequiredCommand 'dotnet' @('--version') $failures
if (-not (Test-Path -LiteralPath 'global.json' -PathType Leaf)) {
    $failures.Add('global.json not found at the repository root')
}
else {
    try {
        $expectedSdk = (Get-Content -LiteralPath 'global.json' -Raw | ConvertFrom-Json -ErrorAction Stop).sdk.version
        if (-not $expectedSdk) {
            $failures.Add('global.json has no sdk.version')
        }
        elseif ($sdkProbe) {
            $selectedSdk = ($sdkProbe.Output -join "`n").Trim()
            if ($selectedSdk -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
                $failures.Add("dotnet --version returned no single SDK version: $selectedSdk")
            }
            else {
                Write-Host "dotnet SDK ${selectedSdk}: selected by global.json (requested ${expectedSdk})"
            }
        }
    }
    catch {
        $failures.Add("global.json could not be read: $_")
    }
}

$nvidiaProbe = Invoke-RequiredCommand 'nvidia-smi' @() $failures
if ($nvidiaProbe) {
    Write-Host 'nvidia-smi: runs'
}

if ($env:APTHERMO_NO_CUDA) {
    $failures.Add("APTHERMO_NO_CUDA is set ('$($env:APTHERMO_NO_CUDA)'); the CUDA tests would refuse the accelerator instead of running on it")
}
else {
    Write-Host 'APTHERMO_NO_CUDA: unset'
}

if ($failures.Count -gt 0) {
    Write-Host '::error::preflight failed:'
    foreach ($failure in $failures) {
        Write-Host "::error::- $failure"
    }
    exit 1
}

Write-Host 'preflight: all checks passed'

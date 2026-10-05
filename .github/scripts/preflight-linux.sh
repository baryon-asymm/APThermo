#!/usr/bin/env bash
# Preflight for the release workflow's Linux (WSL2) GPU job (.github/BOOT.md, "Self-hosted runners"): asserts what the
# runner must provide before any build step runs, and names every missing item instead of letting a later step fail
# on an unhelpful error (the 2026-09-19 post-mortem, "Rehearsal before the tag"). Working directory is the
# repository root (the job's default), so global.json resolves as a relative path.
set -uo pipefail

failures=()

# git
if command -v git >/dev/null 2>&1; then
  echo "git: found ($(git --version))"
else
  failures+=("git not found on PATH")
fi

# dotnet SDK named by global.json
if [[ ! -f global.json ]]; then
  failures+=("global.json not found at the repository root")
else
  expected_sdk=$(grep -o '"version"[[:space:]]*:[[:space:]]*"[^"]*"' global.json | head -1 | sed -E 's/.*"([^"]+)"$/\1/')
  if [[ -z "$expected_sdk" ]]; then
    failures+=("could not read sdk.version from global.json")
  else
    sdks=$(dotnet --list-sdks 2>&1)
    if [[ $? -ne 0 ]]; then
      failures+=("dotnet --list-sdks failed: $sdks")
    elif ! echo "$sdks" | grep -q "$expected_sdk"; then
      failures+=("dotnet SDK $expected_sdk (global.json) not among the installed SDKs: $sdks")
    else
      echo "dotnet SDK $expected_sdk: found"
    fi
  fi
fi

# NVIDIA driver
if nvidia_smi_out=$(nvidia-smi 2>&1); then
  echo "nvidia-smi: runs"
else
  failures+=("nvidia-smi did not run: $nvidia_smi_out")
fi

# APTHERMO_NO_CUDA must be unset, or the CUDA tests only check the refusal
if [[ -n "${APTHERMO_NO_CUDA:-}" ]]; then
  failures+=("APTHERMO_NO_CUDA is set ('$APTHERMO_NO_CUDA'); the CUDA tests would refuse the accelerator instead of running on it")
else
  echo "APTHERMO_NO_CUDA: unset"
fi

if [[ "${#failures[@]}" -gt 0 ]]; then
  echo "::error::preflight failed:"
  for f in "${failures[@]}"; do
    echo "::error::- $f"
  done
  exit 1
fi

echo "preflight: all checks passed"

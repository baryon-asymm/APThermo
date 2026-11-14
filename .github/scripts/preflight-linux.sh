#!/usr/bin/env bash
# Preflight for the release workflow's Linux (WSL2) GPU job (.github/BOOT.md, "Self-hosted runners"): asserts what the
# runner must provide before any build step runs, and names every missing item instead of letting a later step fail
# on an unhelpful error (the 2026-09-19 post-mortem, "Rehearsal before the tag"). Working directory is the
# repository root (the job's default), so global.json resolves as a relative path.
set -uo pipefail

failures=()

# git
if ! command -v git >/dev/null 2>&1; then
  failures+=("git not found on PATH")
elif git_version=$(git --version 2>&1); then
  echo "git: found ($git_version)"
else
  failures+=("git --version failed: $git_version")
fi

# dotnet SDK named by global.json
if [[ ! -f global.json ]]; then
  failures+=("global.json not found at the repository root")
else
  expected_sdk=$(grep -o '"version"[[:space:]]*:[[:space:]]*"[^"]*"' global.json | head -1 | sed -E 's/.*"([^"]+)"$/\1/')
  if [[ -z "$expected_sdk" ]]; then
    failures+=("could not read sdk.version from global.json")
  else
    if ! command -v dotnet >/dev/null 2>&1; then
      failures+=("dotnet not found on PATH")
    elif selected_sdk=$(dotnet --version 2>&1); then
      if [[ "$selected_sdk" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$ ]]; then
        echo "dotnet SDK $selected_sdk: selected by global.json (requested $expected_sdk)"
      else
        failures+=("dotnet --version returned no single SDK version: $selected_sdk")
      fi
    else
      failures+=("dotnet --version failed: $selected_sdk")
    fi
  fi
fi

# NVIDIA driver
if ! command -v nvidia-smi >/dev/null 2>&1; then
  failures+=("nvidia-smi not found on PATH")
elif nvidia_smi_out=$(nvidia-smi 2>&1); then
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

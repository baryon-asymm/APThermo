# API.md — .github/scripts

## Contract ✅

Host preflight and release-note extraction, plus controlled local guard tests.

`preflight-windows.ps1` / `preflight-linux.sh`: run at repository root; exit 0
only when git, the exact SDK from global.json and nvidia-smi run successfully and
APTHERMO_NO_CUDA is unset. On failure exit nonzero and name each missing item.

`check-release.sh <version> <changelog> <notes-output> [ref-name]`: an optional ref
must equal v<version> literally. Extract the exact `## [<version>]` section,
allowing a date suffix, with nonempty notes; failure exits nonzero.

`test_delivery.py`: standard-library local guard suite on hosted Windows/Linux;
controlled command probes, no GPU, publishing or external mutation.

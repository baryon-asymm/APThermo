# API.md — .github/actions/preflight

## Contract ✅

Select the supported host preflight script before self-hosted GPU work.

`action.yml`: composite action with no inputs. On Windows call
`../../scripts/preflight-windows.ps1` under powershell; otherwise call
`../../scripts/preflight-linux.sh` under bash. Resolve paths from github.action_path.

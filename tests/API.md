# API.md — shared test build

## Build contract

`Directory.Build.props` imports the root build policy and defaults child projects to
test projects unless they explicitly opt out with `IsTestProject=false`. Test
projects receive Microsoft.NET.Test.Sdk, xUnit, its Visual Studio runner and the
global `Xunit` using. Versions are inherited from the root package manifest.

There is no exported code declaration in this node. Each child test node's API and
BOOT define its own verification entry points and readiness evidence.

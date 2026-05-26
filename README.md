# Vending Machine

**v0.0.1** — FFXIV Dalamud plugin that automates player trades in two modes:

- **Sell**: Offer configured items, wait until the trade partner offers enough gil, then confirm.
- **Buy**: Read items offered by the partner, pay gil from your price list, then confirm.

## Requirements

- [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher) with Dalamud
- .NET 10 SDK (matches ECommons)
- Windows x64

## Build

Requires .NET 10 SDK and Dalamud dev libs at `%AppData%\XIVLauncher\addon\Hooks\dev\`.

```powershell
git submodule update --init --recursive
dotnet build VendingMachine.sln -c Release
```

Copy the contents of `VendingMachine\bin\Release\` (at minimum `VendingMachine.dll` and `VendingMachine.json`) to `%AppData%\XIVLauncher\devPlugins\VendingMachine\`, or add that folder in `/xlplugins` → Dev Plugin Locations.

## CI / Release (GitHub Actions)

| Workflow | Trigger | Result |
|----------|---------|--------|
| `build.yml` | push / PR to `main` or `master` | Release ビルド + artifact |
| `release.yml` | tag `v*` を push（例: `v0.0.2`） | GitHub Release + `VendingMachine.zip` |

リリース手順:

```powershell
git tag v0.0.2
git push origin v0.0.2
```

CI では [dalamud-distrib](https://goatcorp.github.io/dalamud-distrib/latest.zip) を取得してビルドします。サブモジュール `ECommons` は checkout 時に自動取得されます。

Note: ClickLib is not built as a dependency (incompatible with current Dalamud). Trade confirm uses ECommons `AddonMaster` and `ClickHelper` instead.

## In-game

- `/vendingmachine` or `/vm` — open settings
- **General**: Enable + Sell/Buy mode
- **Sell Setting**: Up to 5 sell lines (inventory search)
- **Buy Setting**: Unlimited buy price entries (all-items search)

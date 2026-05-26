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

Copy `VendingMachine.dll`, `VendingMachine.json`, and `ECommons.dll` from `VendingMachine\bin\Release\` to `%AppData%\XIVLauncher\devPlugins\VendingMachine\` (or add that folder in `/xlplugins` → Dev Plugin Locations). ECommons is linked at compile time and must sit next to the plugin DLL at runtime.

## CI / Release (GitHub Actions)

| Workflow | Trigger | Result |
|----------|---------|--------|
| `build.yml` | push / PR to `main` | ビルド検証 + artifact |
| `release.yml` | `main` への push / tag `v*` / 手動実行 | GitHub Release + `VendingMachine.zip` |

- **`main` に push** → `continuous` タグの pre-release を更新（zip 3 ファイル同梱）
- **バージョン付きリリース** → タグを push:

```powershell
git tag v0.0.2
git push origin v0.0.2
```

Actions 画面から **Release → Run workflow** で手動実行も可能です。

CI では [dalamud-distrib](https://goatcorp.github.io/dalamud-distrib/latest.zip) を取得してビルドします。サブモジュール `ECommons` は checkout 時に自動取得されます。

Note: ClickLib is not built as a dependency (incompatible with current Dalamud). Trade confirm uses ECommons `AddonMaster` and `ClickHelper` instead.

## In-game

- `/vendingmachine` or `/vm` — open settings
- **General**: Enable + Sell/Buy mode
- **Sell Setting**: Up to 5 sell lines (inventory search)
- **Buy Setting**: Unlimited buy price entries (all-items search)

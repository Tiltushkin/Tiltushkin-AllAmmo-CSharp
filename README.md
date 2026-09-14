# Tiltushkin-AllAmmo-CSharp

Updated C# version of the VAFELZ all-ammo trader for **SPT / SP-Tushonka 4.1.x**.

## Compatibility

- Targets `.NET 10`.
- Built against the renamed `SPTushonka.Common`, `SPTushonka.DI`, and `SPTushonka.Server.Core` NuGet packages (`4.1.3`).
- Mod metadata accepts SPT `~4.1.3` (4.1.3 and later compatible 4.1.x releases).

The public C# namespaces remain `SPTarkov.*` in SPT 4.1.x; only the NuGet package IDs were renamed to `SPTushonka.*`.

## 4.0 -> 4.1 migration changes

- `AbstractModMetadata` -> `IModMetadata`.
- `OnLoad()` -> `OnLoadAsync(CancellationToken)`.
- `OnLoadOrder.PostDBModLoader` -> `OnLoadOrder.TraderRegistration + 1` for trader registration.
- Removed `DatabaseService` and `ConfigServer`; tables/configs are injected directly.
- `ModHelper` now comes from `SPTarkov.Server.Core.Helpers.Server`.
- `LocaleService` now comes from `SPTarkov.Server.Core.Services.Locales`.
- `ISptLogger<T>` now comes from `SPTarkov.Common.Models.Logging`.
- Trader DB access now uses `TradersTable`; locales use `LocaleTable`.
- Config file I/O now honors the server cancellation token.

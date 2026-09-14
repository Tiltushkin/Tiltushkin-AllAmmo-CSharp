using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;
using System.Reflection;
using System.Text.Json;
using File = System.IO.File;
using Path = System.IO.Path;

namespace _allAmmo;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.tiltushkin.allammo";
    public string Name { get; init; } = "Tiltushkin-AllAmmo";
    public string Author { get; init; } = "Tiltushkin";
    public List<string>? Contributors { get; init; } = ["Tiltushkin"];
    public SemanticVersioning.Version Version { get; init; } = new("1.5.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.3");
    public List<string>? Incompatibilities { get; init; } = [];
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/Tiltushkin/Tiltushkin-AllAmmo-CSharp/";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 1)]
public class AddTraderWithAssortJson(
    ModHelper modHelper,
    ImageRouter imageRouter,
    TraderConfig traderConfig,
    RagfairConfig ragfairConfig,
    TimeUtil timeUtil,
    AddCustomTraderHelper addCustomTraderHelper,
    LocaleService localeService,
    ISptLogger<AddTraderWithAssortJson> logger
)
    : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var traderImagePath = Path.Combine(pathToMod, "data/vafelz.jpg");
            var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(pathToMod, "data/base.json");

            imageRouter.AddRoute(traderBase.Avatar!.Replace(".jpg", ""), traderImagePath);

            addCustomTraderHelper.SetTraderUpdateTime(
                traderConfig,
                traderBase,
                timeUtil.GetHoursAsSeconds(1),
                timeUtil.GetHoursAsSeconds(2));

            if (!ragfairConfig.Traders.TryAdd(traderBase.Id, true))
            {
                logger.Warning($"[AllAmmo] Trader {traderBase.Id} already in Ragfair config.");
            }

            addCustomTraderHelper.AddTraderWithEmptyAssortToDb(traderBase);
            addCustomTraderHelper.AddTraderToLocales(traderBase, "VAFELZ", "All Ammo Trader.");

            var assort = modHelper.GetJsonDataFromFile<TraderAssort>(pathToMod, "data/assort.json");

            await ProcessConfigurationAsync(pathToMod, assort, cancellationToken);

            addCustomTraderHelper.OverwriteTraderAssort(traderBase.Id, assort);

            logger.Info($"[AllAmmo] Trader {traderBase.Nickname} loaded successfully.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Error($"[AllAmmo] Critical error: {ex.Message ?? "Unknown error"}");
            logger.Error(ex.StackTrace ?? "No stack trace");
        }
    }

    private async Task ProcessConfigurationAsync(string modPath, TraderAssort assort, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(modPath, "config/config.json");
        var configDir = Path.GetDirectoryName(configPath);

        if (configDir != null && !Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        ModConfig config = new();
        bool configNeedsSaving = false;

        if (File.Exists(configPath))
        {
            try
            {
                var jsonContent = await File.ReadAllTextAsync(configPath, cancellationToken);
                config = JsonSerializer.Deserialize<ModConfig>(jsonContent) ?? new ModConfig();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error($"[AllAmmo] Error reading config.json: {ex.Message}");
            }
        }
        else
        {
            configNeedsSaving = true;
        }

        if (!config.EnableConfig)
        {
            return;
        }

        var enDict = localeService.GetLocaleDb("en");

        foreach (var item in assort.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!assort.BarterScheme.ContainsKey(item.Id))
            {
                continue;
            }

            if (!config.Items.ContainsKey(item.Id))
            {
                string itemName = "Unknown Item";

                if (enDict.TryGetValue($"{item.Template} Name", out var name))
                {
                    itemName = name;
                }

                config.Items.Add(item.Id, new ItemSettings
                {
                    ItemName = itemName,
                    PriceMultiplier = 1.0f,
                    StockCount = 0
                });
                configNeedsSaving = true;
            }
        }

        List<string> invalidConfigItems = new();

        foreach (var kvp in config.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var itemId = kvp.Key;
            var settings = kvp.Value;

            var item = assort.Items.FirstOrDefault(x => x.Id == itemId);

            if (item == null)
            {
                logger.Error($"[AllAmmo] Item ID {itemId} ({settings.ItemName}) found in config but missing in assort.json! Marking for removal.");
                invalidConfigItems.Add(itemId);
                continue;
            }

            if (settings.StockCount == -1)
            {
                assort.Items.Remove(item);
                assort.BarterScheme.Remove(itemId);
                assort.LoyalLevelItems.Remove(itemId);
                continue;
            }

            if (settings.StockCount > 0)
            {
                item.Upd ??= new Upd();
                item.Upd.StackObjectsCount = settings.StockCount;
                item.Upd.UnlimitedCount = false;
            }

            if (settings.PriceMultiplier <= 0.001f)
            {
                logger.Warning($"[AllAmmo] PriceMultiplier cannot be 0 for item '{settings.ItemName}'. Using default 1.0.");
                settings.PriceMultiplier = 1.0f;
                configNeedsSaving = true;
            }

            if (Math.Abs(settings.PriceMultiplier - 1.0f) > 0.001f && assort.BarterScheme.TryGetValue(itemId, out var schemes))
            {
                if (schemes.Count > 0 && schemes[0].Count > 0)
                {
                    var priceObj = schemes[0][0];

                    double currentPrice = priceObj.Count.GetValueOrDefault(1.0);
                    double newPrice = currentPrice * settings.PriceMultiplier;

                    priceObj.Count = Math.Max(1, Math.Round(newPrice));
                }
            }
        }

        if (invalidConfigItems.Count > 0)
        {
            foreach (var idToRemove in invalidConfigItems)
            {
                config.Items.Remove(idToRemove);
            }

            configNeedsSaving = true;
            logger.Info($"[AllAmmo] Cleaned up {invalidConfigItems.Count} invalid items from config.");
        }

        if (configNeedsSaving)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, options), cancellationToken);
            logger.Info("[AllAmmo] Config file updated.");
        }
    }
}

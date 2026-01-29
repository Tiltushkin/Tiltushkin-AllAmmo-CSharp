using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using System.Reflection;
using System.Text.Json;
using File = System.IO.File;
using Path = System.IO.Path;

namespace _allAmmo;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.tiltushkin.allammo";
    public override string Name { get; init; } = "Tiltushkin-AllAmmo";
    public override string Author { get; init; } = "Tiltushkin";
    public override List<string>? Contributors { get; init; } = ["Tiltushkin"];
    public override SemanticVersioning.Version Version { get; init; } = new("1.3.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; } = [];
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string Url { get; init; } = "https://github.com/Tiltushkin/Tiltushkin-AllAmmo-CSharp/";
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class AddTraderWithAssortJson(
    ModHelper modHelper,
    ImageRouter imageRouter,
    ConfigServer configServer,
    TimeUtil timeUtil,
    AddCustomTraderHelper addCustomTraderHelper,
    ISptLogger<AddTraderWithAssortJson> logger,
    DatabaseService databaseService
)
    : IOnLoad
{
    public Task OnLoad()
    {
        try
        {

            var traderConfig = configServer.GetConfig<TraderConfig>();
            var ragfairConfig = configServer.GetConfig<RagfairConfig>();

            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var traderImagePath = Path.Combine(pathToMod, "data/vafelz.jpg");
            var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(pathToMod, "data/base.json");

            imageRouter.AddRoute(traderBase.Avatar!.Replace(".jpg", ""), traderImagePath);

            addCustomTraderHelper.SetTraderUpdateTime(traderConfig, traderBase, timeUtil.GetHoursAsSeconds(1), timeUtil.GetHoursAsSeconds(2));

            if (!ragfairConfig.Traders.TryAdd(traderBase.Id, true))
            {
                logger.Warning($"[AllAmmo] Trader {traderBase.Id} already in Ragfair config.");
            }

            addCustomTraderHelper.AddTraderWithEmptyAssortToDb(traderBase);
            addCustomTraderHelper.AddTraderToLocales(traderBase, "VAFELZ", "All Ammo Trader.");

            var assort = modHelper.GetJsonDataFromFile<TraderAssort>(pathToMod, "data/assort.json");

            ProcessConfiguration(pathToMod, assort);

            addCustomTraderHelper.OverwriteTraderAssort(traderBase.Id, assort);

            logger.Info($"[AllAmmo] Trader {traderBase.Nickname} loaded successfully.");
        }
        catch (Exception ex)
        {
            logger.Error($"[AllAmmo] Critical error: {ex.Message ?? "Unknown error"}");
            logger.Error(ex.StackTrace ?? "No stack trace");
        }

        return Task.CompletedTask;
    }

    private void ProcessConfiguration(string modPath, TraderAssort assort)
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
                var jsonContent = File.ReadAllText(configPath);
                config = JsonSerializer.Deserialize<ModConfig>(jsonContent) ?? new ModConfig();
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

        if (!config.EnableConfig) return;

        var globalLocales = databaseService.GetTables().Locales.Global;
        var hasEnLocale = globalLocales.TryGetValue("en", out var enLocaleLazy);

        foreach (var item in assort.Items)
        {
            if (!assort.BarterScheme.ContainsKey(item.Id)) continue;

            if (!config.Items.ContainsKey(item.Id))
            {
                string itemName = "Unknown Item";

                if (hasEnLocale && enLocaleLazy != null)
                {
                    var enDict = enLocaleLazy.Value;
                    if (enDict!.TryGetValue($"{item.Template} Name", out var name))
                    {
                        itemName = name;
                    }
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

        if (configNeedsSaving)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, options));
            logger.Info("[AllAmmo] Config file updated/created.");
        }

        foreach (var kvp in config.Items)
        {
            var itemId = kvp.Key;
            var settings = kvp.Value;

            var item = assort.Items.FirstOrDefault(x => x.Id == itemId);
            if (item == null) continue;

            if (settings.StockCount == -1)
            {
                assort.Items.Remove(item);
                if (assort.BarterScheme.ContainsKey(itemId)) assort.BarterScheme.Remove(itemId);
                if (assort.LoyalLevelItems.ContainsKey(itemId)) assort.LoyalLevelItems.Remove(itemId);
                continue;
            }

            if (settings.StockCount > 0)
            {
                item.Upd.StackObjectsCount = settings.StockCount;
                item.Upd.UnlimitedCount = false;
            }

            if (settings.PriceMultiplier <= 0.001f)
            {
                logger.Warning($"[AllAmmo] PriceMultiplier cannot be 0 for item '{settings.ItemName}'. Using default 1.0.");
                settings.PriceMultiplier = 1.0f;
            }

            if (Math.Abs(settings.PriceMultiplier - 1.0f) > 0.001f && assort.BarterScheme.TryGetValue(itemId, out var schemes))
            {
                if (schemes.Count > 0 && schemes[0].Count > 0)
                {
                    var priceObj = schemes[0][0];

                    double currentPrice = priceObj.Count.GetValueOrDefault(1.0);
                    double newPrice = currentPrice * (double)settings.PriceMultiplier;

                    priceObj.Count = Math.Max(1, Math.Round(newPrice));
                }
            }
        }
    }
}
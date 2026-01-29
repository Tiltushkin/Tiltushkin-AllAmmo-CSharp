using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using System.Reflection;
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
    public override string? Url { get; init; } = "https://github.com/Tiltushkin/Tiltushkin-AllAmmo-CSharp/";
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
    ISptLogger<AddTraderWithAssortJson> logger
)
    : IOnLoad
{
    private readonly TraderConfig _traderConfig = configServer.GetConfig<TraderConfig>();
    private readonly RagfairConfig _ragfairConfig = configServer.GetConfig<RagfairConfig>();

    public Task OnLoad()
    {
        try
        {
            var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var traderImagePath = Path.Combine(pathToMod, "data/vafelz.jpg");
            var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(pathToMod, "data/base.json");
            imageRouter.AddRoute(traderBase.Avatar!.Replace(".jpg", ""), traderImagePath);
            addCustomTraderHelper.SetTraderUpdateTime(_traderConfig, traderBase, timeUtil.GetHoursAsSeconds(1), timeUtil.GetHoursAsSeconds(2));

            if (!_ragfairConfig.Traders.TryAdd(traderBase.Id, true))
            {
                logger.Warning($"[AllAmmo] Trader {traderBase.Id} already in Ragfair config. Possible conflict?");
            }

            addCustomTraderHelper.AddTraderWithEmptyAssortToDb(traderBase);
            addCustomTraderHelper.AddTraderToLocales(traderBase, "VAFELZ", "All Ammo Trader.");

            var assort = modHelper.GetJsonDataFromFile<TraderAssort>(pathToMod, "data/assort.json");
            addCustomTraderHelper.OverwriteTraderAssort(traderBase.Id, assort);

            logger.Info($"[AllAmmo] Trader {traderBase.Nickname} loaded successfully.");
        }
        catch (Exception ex)
        {
            logger.Error($"[AllAmmo] Critical error loading trader: {ex.Message}");
            logger.Error(ex.StackTrace!);
        }

        return Task.CompletedTask;
    }
}
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;

namespace _allAmmo
{
    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
    public class AddCustomTraderHelper(
        ISptLogger<AddCustomTraderHelper> logger,
        ICloner cloner,
        DatabaseService databaseService,
        LocaleService localeService)
    {
        public void SetTraderUpdateTime(TraderConfig traderConfig, TraderBase baseJson, int refreshTimeSecondsMin, int refreshTimeSecondsMax)
        {
            var traderRefreshRecord = new UpdateTime
            {
                TraderId = baseJson.Id,
                Seconds = new MinMax<int>(refreshTimeSecondsMin, refreshTimeSecondsMax)
            };

            traderConfig.UpdateTime.Add(traderRefreshRecord);
        }

        public void AddTraderWithEmptyAssortToDb(TraderBase traderDetailsToAdd)
        {
            var emptyTraderItemAssortObject = new TraderAssort
            {
                Items = [],
                BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
                LoyalLevelItems = new Dictionary<MongoId, int>()
            };

            var traderDataToAdd = new Trader
            {
                Assort = emptyTraderItemAssortObject,
                Base = cloner.Clone(traderDetailsToAdd),
                QuestAssort = new()
                {
                    { "Started", new() },
                    { "Success", new() },
                    { "Fail", new() }
                },
                Dialogue = []
            };

            if (!databaseService.GetTables().Traders.TryAdd(traderDetailsToAdd.Id, traderDataToAdd))
            {
                logger.Error($"[AllAmmo] FAILED to add trader: {traderDetailsToAdd.Id}. This ID is already in use by another mod! Change the _id in base.json.");
            }
        }

        public void AddTraderToLocales(TraderBase baseJson, string firstName, string description)
        {
            var locales = databaseService.GetTables().Locales.Global;
            var newTraderId = baseJson.Id;
            var fullName = baseJson.Name;
            var nickName = baseJson.Nickname;
            var location = baseJson.Location;

            foreach (var (localeKey, localeKvP) in locales)
            {
                localeKvP.AddTransformer(lazyloadedLocaleData =>
                {
                    lazyloadedLocaleData!.TryAdd($"{newTraderId} FullName", fullName!);
                    lazyloadedLocaleData!.TryAdd($"{newTraderId} FirstName", firstName!);
                    lazyloadedLocaleData!.TryAdd($"{newTraderId} Nickname", nickName!);
                    lazyloadedLocaleData!.TryAdd($"{newTraderId} Location", location!);
                    lazyloadedLocaleData!.TryAdd($"{newTraderId} Description", description!);
                    return lazyloadedLocaleData;
                });
            }
        }

        public void OverwriteTraderAssort(string traderId, TraderAssort newAssorts)
        {
            if (!databaseService.GetTables().Traders.TryGetValue(traderId, out var traderToEdit))
            {
                logger.Warning($"[AllAmmo] Cannot add assorts. Trader {traderId} not found in DB.");
                return;
            }

            traderToEdit.Assort = newAssorts;
        }
    }
}
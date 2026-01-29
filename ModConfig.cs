namespace _allAmmo;

public class ModConfig
{
    public bool EnableConfig { get; set; } = true;
    public Dictionary<string, ItemSettings> Items { get; set; } = new();
}

public class ItemSettings
{
    public string ItemName { get; set; } = "Unknown";
    public float PriceMultiplier { get; set; } = 1.0f;
    public int StockCount { get; set; } = 0;
}
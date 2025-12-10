using System;
using System.Text.Json.Serialization;

namespace ItemTracker.Models;

public class Item
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("unit_price")]
    public double UnitPrice { get; set; }

    [JsonPropertyName("stack_size")]
    public int StackSize { get; set; } = 1;

    [JsonPropertyName("weight_per_item")]
    public double WeightPerItem { get; set; } = 1.0;

    [JsonPropertyName("icon_path")]
    public string? IconPath { get; set; }

    [JsonPropertyName("durability")]
    public int? Durability { get; set; }

    [JsonPropertyName("max_durability")]
    public int? MaxDurability { get; set; }

    [JsonIgnore]
    public double PricePerStack => StackSize > 0 ? UnitPrice * StackSize : 0.0;

    [JsonIgnore]
    public double PricePerKg => WeightPerItem > 0 ? UnitPrice / WeightPerItem : 0.0;

    [JsonIgnore]
    public double? DurabilityPercentage
    {
        get
        {
            if (Durability is null || MaxDurability is null || MaxDurability <= 0)
            {
                return null;
            }

            var percentage = Durability.Value / (double)MaxDurability.Value * 100.0;
            return Math.Clamp(percentage, 0, 100);
        }
    }

    [JsonIgnore]
    public double? RoundedDurabilityPercentage =>
        DurabilityPercentage is null
            ? null
            : Math.Clamp(Math.Round(DurabilityPercentage.Value / 5.0, MidpointRounding.AwayFromZero) * 5, 0, 100);
}

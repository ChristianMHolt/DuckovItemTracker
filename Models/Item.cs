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

    [JsonIgnore]
    public double PricePerStack => StackSize > 0 ? UnitPrice * StackSize : 0.0;

    [JsonIgnore]
    public double PricePerKg => WeightPerItem > 0 ? UnitPrice / WeightPerItem : 0.0;
}

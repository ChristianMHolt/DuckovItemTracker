using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using ItemTracker.Models;

namespace ItemTracker.Utilities;

public class ItemSorter : IComparer, IComparer<Item>
{
    private readonly string _field;
    private readonly bool _ascending;

    public ItemSorter(string field, bool ascending)
    {
        _field = field;
        _ascending = ascending;
    }

    public int Compare(object? x, object? y) => Compare(x as Item, y as Item);

    public int Compare(Item? left, Item? right)
    {
        if (left is null || right is null)
        {
            return 0;
        }

        var result = _field switch
        {
            "Unit Price" => left.UnitPrice.CompareTo(right.UnitPrice),
            "Price per Stack" => left.PricePerStack.CompareTo(right.PricePerStack),
            "Price per kg" => left.PricePerKg.CompareTo(right.PricePerKg),
            _ => NaturalCompare(left.Name, right.Name)
        };

        return _ascending ? result : -result;
    }

    private static int NaturalCompare(string a, string b)
    {
        var regex = new Regex("(\\d+)", RegexOptions.Compiled);
        var partsA = regex.Split(a);
        var partsB = regex.Split(b);

        for (var i = 0; i < Math.Max(partsA.Length, partsB.Length); i++)
        {
            if (i >= partsA.Length) return -1;
            if (i >= partsB.Length) return 1;

            var partA = partsA[i];
            var partB = partsB[i];

            var isNumA = int.TryParse(partA, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numA);
            var isNumB = int.TryParse(partB, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numB);

            if (isNumA && isNumB)
            {
                var cmp = numA.CompareTo(numB);
                if (cmp != 0) return cmp;
            }
            else
            {
                var cmp = string.Compare(partA, partB, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
            }
        }

        return 0;
    }
}

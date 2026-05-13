using System.Collections.Frozen;
using System.Globalization;

namespace TransactionApi.Domain;

/// <summary>
/// Validates ISO 4217 alphabetic currency codes using region data plus supplementary fund/precious-metal codes.
/// </summary>
public static class Iso4217Codes
{
    private static readonly FrozenSet<string> Codes = Build();

    private static FrozenSet<string> Build()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                var symbol = region.ISOCurrencySymbol;
                if (symbol.Length == 3)
                    set.Add(symbol);
            }
            catch (ArgumentException)
            {
                // ignore cultures without a region
            }
        }

        return set.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsValid(string? code) =>
        !string.IsNullOrWhiteSpace(code) && Codes.Contains(code.Trim());
}

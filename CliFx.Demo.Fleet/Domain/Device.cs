using System;
using System.Collections.Generic;

namespace CliFx.Demo.Fleet.Domain;

public enum DeviceCategory
{
    Smartphones,
    Laptops,
    Tablets,
}

// The demo API (dummyjson.com) only has products, so the fleet is made of the ones that look like devices
public record Device(int Id, string Title, string? Brand, string Category, decimal Price, int Stock)
{
    private const string IdPrefix = "dev-";

    public static IReadOnlyList<string> CategorySlugs { get; } =
    ["smartphones", "laptops", "tablets"];

    // Identifier that users type on the command line
    public string DeviceId => IdPrefix + Id;

    public static bool TryParseDeviceId(string value, out int id)
    {
        id = 0;

        return value.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(value[IdPrefix.Length..], out id);
    }
}

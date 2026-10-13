using System;
using System.IO;

namespace CliFx.Demo.Fleet.Services;

// Location of the files that the application keeps between runs
public class FleetPaths
{
    public string Root { get; } =
        Environment.GetEnvironmentVariable("FLEET_HOME") is { Length: > 0 } customRoot
            ? customRoot
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CliFx.Demo.Fleet"
            );

    public string SessionFile => Path.Combine(Root, "session.json");

    public string CacheDirectory => Path.Combine(Root, "cache");
}

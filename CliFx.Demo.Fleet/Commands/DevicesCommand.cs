using System;
using System.Linq;
using System.Threading.Tasks;
using CliFx.Binding;
using CliFx.Demo.Fleet.Domain;
using CliFx.Demo.Fleet.Services;
using CliFx.Infrastructure;

namespace CliFx.Demo.Fleet.Commands;

[Command("devices", Description = "Lists devices in the fleet.")]
public partial class DevicesCommand(FleetApiClient api, SessionStore sessions) : ICommand
{
    // The set of allowed values is inferred from the enum, no completion provider required
    [CommandOption("category", 'c', Description = "Only show devices of this category.")]
    public DeviceCategory? Category { get; set; }

    [CommandOption("search", 's', Description = "Only show devices that match this text.")]
    public string? Search { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        var session = sessions.TryLoad();
        if (session is null || session.IsExpired)
            throw new CommandException("Not signed in. Run `fleet login` first.", 10);

        try
        {
            var devices = (
                await api.SearchDevicesAsync(session.AccessToken, Search, cancellationToken)
            )
                .Where(d =>
                    Category is null
                    || string.Equals(
                        d.Category,
                        Category.ToString(),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .ToArray();

            foreach (var device in devices)
                console.WriteLine(
                    $"{device.DeviceId, -9} {device.Title, -45} {device.Category, -12} {device.Stock, 4} in stock"
                );

            console.WriteLine();
            console.WriteLine($"{devices.Length} device(s).");
        }
        catch (FleetApiException ex)
        {
            throw new CommandException(ex.Message, 10);
        }
    }
}

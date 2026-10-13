using System.Threading.Tasks;
using CliFx.Binding;
using CliFx.Demo.Fleet.Completion;
using CliFx.Demo.Fleet.Domain;
using CliFx.Demo.Fleet.Services;
using CliFx.Infrastructure;

namespace CliFx.Demo.Fleet.Commands;

[Command("device", Description = "Shows details of a device.")]
public partial class DeviceCommand(FleetApiClient api, SessionStore sessions) : ICommand
{
    [CommandParameter(
        0,
        Name = "device",
        Description = "Device ID.",
        CompletionProvider = typeof(DeviceCompletionProvider)
    )]
    public required string DeviceId { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        if (!Device.TryParseDeviceId(DeviceId, out var id))
            throw new CommandException(
                $"'{DeviceId}' is not a valid device ID (expected: dev-123).",
                10
            );

        // Permissions are verified again when the command runs, regardless of what completion suggested
        var session = sessions.TryLoad();
        if (session is null || session.IsExpired)
            throw new CommandException("Not signed in. Run `fleet login` first.", 10);

        try
        {
            var device =
                await api.TryGetDeviceAsync(session.AccessToken, id, cancellationToken)
                ?? throw new CommandException($"Device '{DeviceId}' not found.", 10);

            console.WriteLine(device.Title);
            console.WriteLine($"  ID:       {device.DeviceId}");
            console.WriteLine($"  Brand:    {device.Brand ?? "-"}");
            console.WriteLine($"  Category: {device.Category}");
            console.WriteLine($"  Price:    {device.Price:0.00} USD");
            console.WriteLine($"  In stock: {device.Stock}");
        }
        catch (FleetApiException ex)
        {
            throw new CommandException(ex.Message, 10);
        }
    }
}

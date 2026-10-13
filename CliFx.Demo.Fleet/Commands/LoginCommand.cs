using System.Threading.Tasks;
using CliFx.Binding;
using CliFx.Demo.Fleet.Services;
using CliFx.Infrastructure;

namespace CliFx.Demo.Fleet.Commands;

[Command("login", Description = "Signs in to the fleet API.")]
public partial class LoginCommand(
    FleetApiClient api,
    SessionStore sessions,
    DeviceSuggestionCache cache
) : ICommand
{
    [CommandOption(
        "username",
        'u',
        EnvironmentVariable = "FLEET_USERNAME",
        Description = "User name."
    )]
    public required string Username { get; set; }

    // Passing secrets as command line arguments leaks them into shell history, hence the environment variable
    [CommandOption(
        "password",
        'p',
        EnvironmentVariable = "FLEET_PASSWORD",
        Description = "Password."
    )]
    public required string Password { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        try
        {
            var session = await api.LoginAsync(Username, Password, cancellationToken);

            // Suggestions that were cached for a previous session must not leak into this one
            cache.Clear();
            sessions.Save(session);

            console.WriteLine(
                $"Signed in as {session.Username} (session expires {session.ExpiresAt.LocalDateTime:t})."
            );
        }
        catch (FleetApiException ex)
        {
            throw new CommandException(ex.Message, 10);
        }
    }
}

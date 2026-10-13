using System.Threading.Tasks;
using CliFx.Binding;
using CliFx.Demo.Fleet.Services;
using CliFx.Infrastructure;

namespace CliFx.Demo.Fleet.Commands;

[Command(
    "logout",
    Description = "Signs out and forgets everything that was cached for the session."
)]
public partial class LogoutCommand(SessionStore sessions, DeviceSuggestionCache cache) : ICommand
{
    public ValueTask ExecuteAsync(IConsole console)
    {
        sessions.Delete();
        cache.Clear();

        console.WriteLine("Signed out.");

        return default;
    }
}

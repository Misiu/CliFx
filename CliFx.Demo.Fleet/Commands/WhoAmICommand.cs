using System.Threading.Tasks;
using CliFx.Binding;
using CliFx.Demo.Fleet.Services;
using CliFx.Infrastructure;

namespace CliFx.Demo.Fleet.Commands;

[Command("whoami", Description = "Shows the signed-in user.")]
public partial class WhoAmICommand(SessionStore sessions) : ICommand
{
    public ValueTask ExecuteAsync(IConsole console)
    {
        var session = sessions.TryLoad();
        if (session is null)
            throw new CommandException("Not signed in. Run `fleet login` first.", 10);

        console.WriteLine(
            session.IsExpired
                ? $"{session.Username} (session expired, run `fleet login` again)"
                : $"{session.Username} (session expires {session.ExpiresAt.LocalDateTime:t})"
        );

        return default;
    }
}

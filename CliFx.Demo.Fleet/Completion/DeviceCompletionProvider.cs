using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliFx.Completion;
using CliFx.Demo.Fleet.Services;

namespace CliFx.Demo.Fleet.Completion;

// Suggests the devices that the signed-in user has access to.
//
// This runs on every Tab press, so it follows a few rules:
// - Never prompt for anything: no session, or an expired one, simply means no suggestions
// - Never fail loudly: an API error means no suggestions, not a stack trace in the middle of the prompt
// - Never show protected data when the API says the token is no longer valid
// - Let the API do the searching, so that large fleets don't have to be downloaded to find "ab..."
public class DeviceCompletionProvider(
    FleetApiClient api,
    SessionStore sessions,
    DeviceSuggestionCache cache
) : ICompletionProvider
{
    public async ValueTask<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken
    )
    {
        var session = sessions.TryLoad();
        if (session is null || session.IsExpired)
            return [];

        var query = context.WordToComplete;

        var devices = cache.TryGet(session.UserId, query);
        if (devices is null)
        {
            try
            {
                devices = await api.SearchDevicesAsync(
                    session.AccessToken,
                    query,
                    cancellationToken
                );
            }
            catch (FleetApiException ex) when (ex.IsUnauthorized)
            {
                // Access has been revoked, so anything that was cached is no longer trustworthy
                cache.Clear();
                return [];
            }

            cache.Set(session.UserId, query, devices);
        }

        return devices
            .Take(30)
            .Select(d => new CompletionItem(
                // What gets inserted into the command line
                d.DeviceId,
                // What the user sees in the list of suggestions
                d.Title,
                $"{d.Brand ?? "Unknown brand"}, {d.Category}, {d.Stock} in stock"
            ))
            .ToArray();
    }
}

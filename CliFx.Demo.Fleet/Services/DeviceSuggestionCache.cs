using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CliFx.Demo.Fleet.Domain;

namespace CliFx.Demo.Fleet.Services;

// Completion runs in a new process on every key press, so an in-memory cache would never be hit.
// This cache lives on disk instead, with the following rules:
// - Entries are scoped to the signed-in user, so one user can never see another user's devices
// - Entries are short-lived (30 seconds by default), so that provisioned/renamed/removed devices show up quickly
// - Everything is wiped on login, logout, and when the API rejects the token
// - Setting FLEET_COMPLETION_CACHE_SECONDS to 0 disables caching altogether, which is the safer
//   choice for sensitive data (revoked permissions don't invalidate what has already been cached)
public class DeviceSuggestionCache(FleetPaths paths)
{
    private static TimeSpan GetTimeToLive() =>
        int.TryParse(
            Environment.GetEnvironmentVariable("FLEET_COMPLETION_CACHE_SECONDS"),
            out var seconds
        )
            ? TimeSpan.FromSeconds(Math.Max(0, seconds))
            : TimeSpan.FromSeconds(30);

    private string GetEntryPath(int userId, string query) =>
        Path.Combine(
            paths.CacheDirectory,
            userId.ToString(),
            Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(query.ToLowerInvariant())))
                + ".json"
        );

    public IReadOnlyList<Device>? TryGet(int userId, string query)
    {
        try
        {
            var timeToLive = GetTimeToLive();
            var path = GetEntryPath(userId, query);

            if (timeToLive <= TimeSpan.Zero || !File.Exists(path))
                return null;

            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > timeToLive)
                return null;

            return JsonSerializer.Deserialize<Device[]>(File.ReadAllBytes(path));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Set(int userId, string query, IReadOnlyList<Device> devices)
    {
        try
        {
            if (GetTimeToLive() <= TimeSpan.Zero)
                return;

            var path = GetEntryPath(userId, query);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(devices));
        }
        // Caching is best-effort
        catch (Exception) { }
    }

    public void Clear()
    {
        try
        {
            if (Directory.Exists(paths.CacheDirectory))
                Directory.Delete(paths.CacheDirectory, true);
        }
        catch (Exception) { }
    }
}

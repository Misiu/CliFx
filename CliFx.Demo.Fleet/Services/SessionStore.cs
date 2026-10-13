using System;
using System.IO;
using System.Text.Json;

namespace CliFx.Demo.Fleet.Services;

public record Session(int UserId, string Username, string AccessToken, DateTimeOffset ExpiresAt)
{
    public bool IsExpired => ExpiresAt <= DateTimeOffset.UtcNow;
}

// Keeps the session between runs.
//
// WARNING: for the sake of keeping this demo free of dependencies, the access token is stored as PLAIN TEXT.
// A real application must use a secure store instead, such as Windows Credential Manager / DPAPI, macOS Keychain,
// or libsecret on Linux, and must never keep tokens in an unprotected file.
public class SessionStore(FleetPaths paths)
{
    public Session? TryLoad()
    {
        try
        {
            return File.Exists(paths.SessionFile)
                ? JsonSerializer.Deserialize<Session>(File.ReadAllBytes(paths.SessionFile))
                : null;
        }
        // Unreadable session is the same as no session
        catch (Exception)
        {
            return null;
        }
    }

    public void Save(Session session)
    {
        Directory.CreateDirectory(paths.Root);
        File.WriteAllBytes(paths.SessionFile, JsonSerializer.SerializeToUtf8Bytes(session));

        // At the very least, don't let other users read the file
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(paths.SessionFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public void Delete()
    {
        if (File.Exists(paths.SessionFile))
            File.Delete(paths.SessionFile);
    }
}

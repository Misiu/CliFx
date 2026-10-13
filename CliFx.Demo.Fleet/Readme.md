# CliFx Fleet Demo

Sample command-line interface for managing a fleet of devices, built to showcase **shell completion** in CliFx
(proof of concept for [Tyrrrz/CliFx#13](https://github.com/Tyrrrz/CliFx/issues/13)).

It demonstrates two kinds of suggestions:

- **Static**, derived from command metadata: command names (including multi-word ones), option names, and enum values.
- **Dynamic**, produced at the moment you press <kbd>Tab</kbd> by calling a REST API as the signed-in user.

```console
$ fleet dev<TAB>              →  device  devices
$ fleet devices --cat<TAB>    →  --category
$ fleet devices -c <TAB>      →  Smartphones  Laptops  Tablets
$ fleet device iph<TAB>       →  dev-123 (iPhone 13 Pro)  dev-121 (iPhone 5s)  dev-122 (iPhone 6) ...
```

The last one is the interesting part: the API is asked for devices matching `iph`, but only after you have signed in
with `fleet login`, and only for the devices that your account is allowed to see.
Before signing in (or after the session expires, or when the API is down) there are simply no suggestions — completion never prompts and never prints errors.

## Commands

| Command                                  | Description                                                          |
| ---------------------------------------- | -------------------------------------------------------------------- |
| `fleet login -u <user> -p <password>`    | Signs in. Credentials can also come from `FLEET_USERNAME` / `FLEET_PASSWORD`. |
| `fleet logout`                           | Signs out and wipes the suggestion cache.                            |
| `fleet whoami`                           | Shows the signed-in user.                                            |
| `fleet devices [-c <category>] [-s <text>]` | Lists devices.                                                    |
| `fleet device <id>`                      | Shows a device. **`<id>` is completed dynamically.**                 |
| `fleet [completion] <shell>`             | Prints the completion script for `powershell`, `bash` or `zsh`.      |

## Where the data comes from

The demo talks to [dummyjson.com](https://dummyjson.com), a free fake REST API.
Its "products" from the smartphone, laptop and tablet categories play the role of devices, and its token-protected `/auth/*` endpoints
play the role of an API that requires authentication.
You can sign in with any of the demo accounts listed in the [dummyjson docs](https://dummyjson.com/docs/auth).

To use a different API that has the same shape, set `FLEET_API_URL`.

## Try it

You need the .NET SDK. From the repository root:

```powershell
dotnet build CliFx.Demo.Fleet -c Release

# Make `fleet` available in this session
$env:PATH += ";$PWD\CliFx.Demo.Fleet\bin\Release\net10.0"

# Enable completion in this session
fleet '[completion]' powershell | Out-String | Invoke-Expression

# Sign in (use an account from https://dummyjson.com/docs/auth)
fleet login -u <username> -p <password>
```

Now type `fleet device ` followed by a few letters and press <kbd>Tab</kbd>.

> [!TIP]
> By default PowerShell cycles through the inserted values (`dev-123`, `dev-121`, ...) one at a time.
> To see the human-readable names and descriptions in a menu, add this to your profile:
>
> ```powershell
> Set-PSReadLineKeyHandler -Key Tab -Function MenuComplete
> ```
>
> The menu shows `iPhone 13 Pro` with the description `Apple, smartphones, 56 in stock` below, while `dev-123` is what ends up on the command line.

## Install

### 1. Install the executable

```powershell
dotnet publish CliFx.Demo.Fleet -c Release -o $HOME\fleet
```

Then add `$HOME\fleet` to your `PATH` (on Linux/macOS: publish somewhere that is already on it, e.g. `-o ~/.local/bin`).

> [!NOTE]
> Every <kbd>Tab</kbd> press starts a new `fleet` process, so startup time matters.
> A framework-dependent build is fine for a demo, but an application that is meant to be completed a lot should consider
> ReadyToRun (`-p:PublishReadyToRun=true -r win-x64`) or Native AOT, as the other CliFx demo does.

### 2. Install the completion script

CliFx only **generates** the script and leaves the decision of where to put it to the application (or its installer).
Generate it once, save it to a file, and load that file from your shell profile — don't run `fleet` from the profile itself, as that would slow down every new shell.

**PowerShell** (Windows PowerShell 5.1 and PowerShell 7+)

```powershell
fleet '[completion]' powershell | Set-Content -Encoding utf8 $HOME\fleet\fleet-completion.ps1
Add-Content $PROFILE '. "$HOME\fleet\fleet-completion.ps1"'
```

**Bash**

```bash
fleet '[completion]' bash > ~/.fleet-completion.bash
echo 'source ~/.fleet-completion.bash' >> ~/.bashrc
```

**Zsh** (the line must come after `compinit`)

```zsh
fleet '[completion]' zsh > ~/.fleet-completion.zsh
echo 'source ~/.fleet-completion.zsh' >> ~/.zshrc
```

Quote `'[completion]'` in Bash and Zsh, otherwise the shell treats it as a glob pattern.
Regenerate the file whenever the commands or options of the application change.

## How it works

1. `AllowCompletion()` in [Program.cs](Program.cs) turns on two directives:
   - `fleet [completion] <shell>` prints a small script that registers a completer with the shell
     (`Register-ArgumentCompleter -Native` for PowerShell, `complete -F` for Bash, `compdef` for Zsh).
   - `fleet [suggest] "<text typed up to the cursor>"` is what the script calls on every <kbd>Tab</kbd>.
     It prints one suggestion per line as `value<TAB>display text<TAB>description`.
2. CliFx parses the text, figures out what is being completed (a command name, an option, or the value of a specific parameter/option)
   and either answers from command metadata or asks the input's **completion provider**.
3. The provider for `fleet device <id>` is attached declaratively, next to the converters and validators:

   ```csharp
   [CommandParameter(0, Name = "device", CompletionProvider = typeof(DeviceCompletionProvider))]
   public required string DeviceId { get; set; }
   ```

4. [DeviceCompletionProvider](Completion/DeviceCompletionProvider.cs) is created through the same `ITypeInstantiator` as commands, so it gets
   the API client, session store and cache from the DI container.
   It returns `CompletionItem(value: "dev-123", display: "iPhone 13 Pro", description: "Apple, smartphones, 56 in stock")`.

### Security and caching

Completion runs in a new process on every key press, so an in-memory cache would never be hit and a naive implementation would call the API every time.
Caching is the application's job, and this demo shows one reasonable approach:

- **The access token is stored as plain text** in `session.json` ([SessionStore](Services/SessionStore.cs)), purely to keep the demo free of extra dependencies.
  A real application must keep it in a secure store instead: Windows Credential Manager/DPAPI, macOS Keychain, or libsecret on Linux.
- Suggestions are cached on disk for **30 seconds**, in a directory scoped to the signed-in user ([DeviceSuggestionCache](Services/DeviceSuggestionCache.cs)).
  Set `FLEET_COMPLETION_CACHE_SECONDS` to change the TTL, or to `0` to disable caching for sensitive data — revoking someone's permissions
  on the server does not remove device names that were already cached on their machine.
- The cache is wiped on `login`, `logout`, and when the API answers `401`/`403`.
- No session, an expired session, a rejected token, an unreachable API, or a timeout (3 seconds by default) all result in **no suggestions**, without prompts or error output.
- Permissions are checked again when the command itself runs; completion is a convenience, not an authorization mechanism.

Files are kept in `%LOCALAPPDATA%\CliFx.Demo.Fleet` (override with `FLEET_HOME`).

## Known limitations

- **PowerShell** doesn't invoke completers for a bare `-` or `--` (Windows PowerShell 5.1), so type at least one letter of the option name: `--cat<Tab>`. PowerShell 7 handles `-c<Tab>`.
  When there are no suggestions at all, PowerShell falls back to completing file names.
- **Bash** can only show the inserted values (`dev-123`), not the names or descriptions that go with them.
  Because the values are IDs that don't start with what was typed (`iph`), Bash's default behavior of inserting the longest common prefix replaces the typed text.
  `bind 'TAB:menu-complete'` makes Tab cycle through the candidates instead, which is closer to what PowerShell does.
- **Zsh** shows names and descriptions next to the values, and the generated script switches to menu completion when the suggested values don't start with the typed text,
  so `fleet device iph<Tab>` lists the matching devices instead of collapsing them to a common prefix.
- **CMD** is not supported, as it has no API for registering custom completions of external programs.

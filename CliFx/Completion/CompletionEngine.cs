using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliFx.Binding;
using CliFx.Infrastructure;
using CliFx.Parsing;

namespace CliFx.Completion;

/// <summary>
/// Calculates suggestions for a partially typed command line.
/// </summary>
internal class CompletionEngine(
    CommandRootDescriptor root,
    ITypeInstantiator typeInstantiator,
    IReadOnlyDictionary<string, string> environmentVariables
)
{
    private static bool IsOptionIdentifierPrefix(string word) =>
        // Name
        word.StartsWith("--", StringComparison.Ordinal)
            && (word.Length == 2 || char.IsLetter(word[2]))
        ||
        // Short name
        word.StartsWith('-') && (word.Length == 1 || char.IsLetter(word[1]));

    private static bool IsBoolean(CommandInputDescriptor input) =>
        input.Property.Type == typeof(bool)
        || Nullable.GetUnderlyingType(input.Property.Type) == typeof(bool);

    private static bool StartsWith(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    // Suggests the next word of the command name, e.g. for "book" -> "add", "list", "remove"
    private IEnumerable<CompletionItem> GetCommandNameItems(
        string? parentName,
        string wordToComplete
    )
    {
        var parentWordCount = parentName?.Split(' ').Length ?? 0;
        var candidates = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var command in root.Commands)
        {
            if (string.IsNullOrWhiteSpace(command.Name))
                continue;

            if (
                parentName is not null
                && !command.Name.StartsWith(parentName + ' ', StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            var words = command.Name.Split(' ');
            if (words.Length <= parentWordCount)
                continue;

            var nextWord = words[parentWordCount];
            if (!StartsWith(nextWord, wordToComplete))
                continue;

            // Description is only meaningful if the word is a complete command name on its own
            var description = words.Length == parentWordCount + 1 ? command.Description : null;
            if (!candidates.TryGetValue(nextWord, out var existing) || existing is null)
                candidates[nextWord] = description;
        }

        return candidates
            .OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(c => new CompletionItem(c.Key, description: c.Value));
    }

    private static IEnumerable<CompletionItem> GetOptionNameItems(
        CommandDescriptor command,
        ParsedCommandLine commandLine,
        string wordToComplete
    )
    {
        foreach (var option in command.Options)
        {
            // Skip options that have already been specified, unless they accept multiple values
            var isUsed = commandLine.Options.Any(o => option.MatchesIdentifier(o.Identifier));
            if (isUsed && !option.IsSequenceBased)
                continue;

            if (
                !string.IsNullOrWhiteSpace(option.Name)
                && StartsWith("--" + option.Name, wordToComplete)
            )
            {
                yield return new CompletionItem(
                    "--" + option.Name,
                    description: option.Description
                );
            }

            if (option.ShortName is not null && StartsWith("-" + option.ShortName, wordToComplete))
            {
                yield return new CompletionItem(
                    "-" + option.ShortName,
                    description: option.Description
                );
            }
        }
    }

    private async ValueTask<IReadOnlyList<CompletionItem>> GetInputValueItemsAsync(
        CommandDescriptor command,
        CommandInputDescriptor input,
        string wordToComplete,
        IReadOnlyList<string> precedingArguments,
        CancellationToken cancellationToken
    )
    {
        // Custom provider
        if (input.CompletionProviderType is { } providerType)
        {
            try
            {
                if (
                    typeInstantiator.CreateInstance(providerType)
                    is not ICompletionProvider provider
                )
                    return [];

                var context = new CompletionContext(
                    command,
                    input,
                    wordToComplete,
                    precedingArguments,
                    environmentVariables
                );

                return await provider.GetCompletionsAsync(context, cancellationToken);
            }
            // Completion must be tolerant of failures (unreachable API, expired credentials, etc.),
            // so that they don't prevent the remaining suggestions from being shown.
            catch (Exception)
            {
                return [];
            }
        }

        // Inferred from the property type (enum members)
        return input
                .Property.TryGetValidValues()
                ?.OfType<string>()
                .Where(v => StartsWith(v, wordToComplete))
                .Select(v => new CompletionItem(v))
                .ToArray()
            ?? [];
    }

    /// <summary>
    /// Gets suggestions for the specified command line text, which is expected to contain
    /// everything the user has typed up to the cursor, starting with the executable name.
    /// </summary>
    public async ValueTask<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        string commandLineText,
        CancellationToken cancellationToken
    )
    {
        // Drop the executable name
        var words = CommandLineTokenizer
            .Split(commandLineText, out var isLastWordUnfinished)
            .Skip(1)
            .ToArray();

        var wordToComplete = isLastWordUnfinished ? words[^1] : "";
        var precedingArguments = isLastWordUnfinished ? words[..^1] : words;

        var commandLine = ParsedCommandLine.Parse(precedingArguments, root.GetCommandNames());

        var command = !string.IsNullOrWhiteSpace(commandLine.CommandName)
            ? root.TryFindCommand(commandLine.CommandName)
            : root.TryFindDefaultCommand();

        var result = new List<CompletionItem>();

        // Option name
        if (IsOptionIdentifierPrefix(wordToComplete))
        {
            if (command is not null)
                result.AddRange(GetOptionNameItems(command, commandLine, wordToComplete));

            return result;
        }

        // Option value, or the next option if the last one is already satisfied
        if (commandLine.Options.LastOrDefault() is { } lastParsedOption)
        {
            var option = command?.Options.FirstOrDefault(o =>
                o.MatchesIdentifier(lastParsedOption.Identifier)
            );

            if (command is null || option is null)
                return result;

            var isSatisfied =
                IsBoolean(option) || !option.IsSequenceBased && lastParsedOption.Values.Count > 0;

            if (isSatisfied)
            {
                if (wordToComplete.Length == 0)
                    result.AddRange(GetOptionNameItems(command, commandLine, wordToComplete));

                return result;
            }

            result.AddRange(
                await GetInputValueItemsAsync(
                    command,
                    option,
                    wordToComplete,
                    precedingArguments,
                    cancellationToken
                )
            );

            return result;
        }

        // Positional: subcommand name and/or parameter value
        if (commandLine.PositionalArguments.Count == 0)
            result.AddRange(GetCommandNameItems(commandLine.CommandName, wordToComplete));

        if (command is not null)
        {
            var parameters = command.Parameters.OrderBy(p => p.Order).ToArray();
            var position = commandLine.PositionalArguments.Count;

            var parameter =
                position < parameters.Length
                    ? parameters[position]
                    : parameters.LastOrDefault(p => p.IsSequenceBased);

            if (parameter is not null)
            {
                result.AddRange(
                    await GetInputValueItemsAsync(
                        command,
                        parameter,
                        wordToComplete,
                        precedingArguments,
                        cancellationToken
                    )
                );
            }
        }

        return result;
    }
}

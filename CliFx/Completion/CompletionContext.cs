using System.Collections.Generic;
using CliFx.Binding;

namespace CliFx.Completion;

/// <summary>
/// Information about the command-line input that is being completed.
/// </summary>
public class CompletionContext(
    CommandDescriptor command,
    CommandInputDescriptor input,
    string wordToComplete,
    IReadOnlyList<string> precedingArguments,
    IReadOnlyDictionary<string, string> environmentVariables
)
{
    /// <summary>
    /// Command whose input is being completed.
    /// </summary>
    public CommandDescriptor Command { get; } = command;

    /// <summary>
    /// Input (parameter or option) whose value is being completed.
    /// </summary>
    public CommandInputDescriptor Input { get; } = input;

    /// <summary>
    /// Text that the user has typed so far for the value that is being completed.
    /// Empty if the user has not started typing the value yet.
    /// </summary>
    public string WordToComplete { get; } = wordToComplete;

    /// <summary>
    /// Arguments that precede <see cref="WordToComplete" /> on the command line,
    /// not including the executable name.
    /// </summary>
    public IReadOnlyList<string> PrecedingArguments { get; } = precedingArguments;

    /// <summary>
    /// Environment variables available to the application.
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; } = environmentVariables;
}

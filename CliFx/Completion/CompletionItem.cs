using System;

namespace CliFx.Completion;

/// <summary>
/// Single suggestion produced during shell completion.
/// </summary>
public class CompletionItem(string value, string? display = null, string? description = null)
{
    /// <summary>
    /// Text that is inserted into the command line when the suggestion is accepted.
    /// </summary>
    public string Value { get; } =
        !string.IsNullOrEmpty(value)
            ? value
            : throw new ArgumentException("Completion value cannot be empty.", nameof(value));

    /// <summary>
    /// Text that is shown to the user in the list of suggestions.
    /// If not specified, <see cref="Value" /> is used.
    /// </summary>
    /// <remarks>
    /// Not all shells can display text that differs from the inserted value.
    /// </remarks>
    public string Display { get; } = !string.IsNullOrWhiteSpace(display) ? display : value;

    /// <summary>
    /// Optional human-readable description of the suggestion.
    /// </summary>
    public string? Description { get; } = description;

    /// <inheritdoc />
    public override string ToString() => Value;
}

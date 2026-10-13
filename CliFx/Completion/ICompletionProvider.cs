using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CliFx.Completion;

/// <summary>
/// Provides suggestions for the value of a command input during shell completion.
/// </summary>
/// <remarks>
/// <para>
/// Completion providers are instantiated through <see cref="Infrastructure.ITypeInstantiator" />,
/// which means they can take dependencies (API clients, authentication services, caches, etc.)
/// if the application uses a dependency injection container.
/// </para>
/// <para>
/// Completion runs in a separate process launched by the shell, on every key press.
/// Implementations must be fast, must never prompt the user for input, and should return
/// an empty list (instead of throwing) when suggestions cannot be provided.
/// </para>
/// <para>
/// Suggestions are NOT filtered by the framework — implementations are expected to use
/// <see cref="CompletionContext.WordToComplete" /> to narrow down the results.
/// </para>
/// </remarks>
public interface ICompletionProvider
{
    /// <summary>
    /// Gets suggestions for the input described by the specified context.
    /// </summary>
    ValueTask<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken
    );
}

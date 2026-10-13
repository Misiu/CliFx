using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CliFx.Generators;
using CliFx.Tests.Utils;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace CliFx.Tests;

public class CompletionSpecs(ITestOutputHelper testOutput) : SpecsBase(testOutput)
{
    private const string CommandsSourceCode =
        // lang=csharp
        """
            public enum DeviceColor { Red, Green, Blue }

            public class DeviceProvider : ICompletionProvider
            {
                public ValueTask<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
                    CompletionContext context,
                    System.Threading.CancellationToken cancellationToken
                )
                {
                    IReadOnlyList<CompletionItem> items = new[]
                    {
                        new CompletionItem("id-1", "Phone One", "First phone"),
                        new CompletionItem("id-2", "Phone Two"),
                        new CompletionItem("id-3", "Laptop Three", "Third\tdevice\r\nwith a messy description"),
                    }
                    .Where(i => i.Display.StartsWith(context.WordToComplete, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                    return new(items);
                }
            }

            public class EchoProvider : ICompletionProvider
            {
                public ValueTask<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
                    CompletionContext context,
                    System.Threading.CancellationToken cancellationToken
                )
                {
                    IReadOnlyList<CompletionItem> items =
                    [
                        new CompletionItem(
                            string.Join(
                                "|",
                                context.Command.Name,
                                context.Input.Property.Name,
                                context.WordToComplete,
                                string.Join(",", context.PrecedingArguments)
                            )
                        )
                    ];

                    return new(items);
                }
            }

            public class FailingProvider : ICompletionProvider
            {
                public ValueTask<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
                    CompletionContext context,
                    System.Threading.CancellationToken cancellationToken
                ) => throw new InvalidOperationException("The API is down.");
            }

            public class HangingProvider : ICompletionProvider
            {
                public async ValueTask<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
                    CompletionContext context,
                    System.Threading.CancellationToken cancellationToken
                )
                {
                    // Intentionally ignores the cancellation token
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    return [new CompletionItem("too-late")];
                }
            }

            [Command("device", Description = "Shows a device.")]
            public partial class DeviceCommand : ICommand
            {
                [CommandParameter(0, Name = "id", CompletionProvider = typeof(DeviceProvider))]
                public required string Id { get; set; }

                [CommandOption("verbose", 'v', Description = "Prints more details.")]
                public bool IsVerbose { get; set; }

                [CommandOption("color", 'c', Description = "Device color.")]
                public DeviceColor? Color { get; set; }

                [CommandOption("tag", 't', Description = "Device tags.", CompletionProvider = typeof(EchoProvider))]
                public IReadOnlyList<string> Tags { get; set; } = [];

                public ValueTask ExecuteAsync(IConsole console) => default;
            }

            [Command("device reboot", Description = "Reboots a device.")]
            public partial class DeviceRebootCommand : ICommand
            {
                [CommandParameter(0, Name = "id", CompletionProvider = typeof(DeviceProvider))]
                public required string Id { get; set; }

                public ValueTask ExecuteAsync(IConsole console) => default;
            }

            [Command("devices", Description = "Lists devices.")]
            public partial class DevicesCommand : ICommand
            {
                [CommandParameter(0, Name = "mode", CompletionProvider = typeof(EchoProvider))]
                public string? Mode { get; set; }

                public ValueTask ExecuteAsync(IConsole console) => default;
            }

            [Command("login", Description = "Logs in.")]
            public partial class LoginCommand : ICommand
            {
                public ValueTask ExecuteAsync(IConsole console) => default;
            }

            [Command("flaky")]
            public partial class FlakyCommand : ICommand
            {
                [CommandParameter(0, Name = "broken", CompletionProvider = typeof(FailingProvider))]
                public required string Broken { get; set; }

                [CommandOption("slow", CompletionProvider = typeof(HangingProvider))]
                public string? Slow { get; set; }

                public ValueTask ExecuteAsync(IConsole console) => default;
            }
            """;

    // The fake console accumulates output, so we only read what was written since the last call
    private int _readOutputLength;

    private CommandLineApplicationBuilder CreateBuilder() =>
        new CommandLineApplicationBuilder()
            .AddCommands(CommandCompiler.Compile(CommandsSourceCode))
            .SetExecutableName("app")
            .UseConsole(FakeConsole);

    private async Task<IReadOnlyList<string[]>> SuggestAsync(
        CommandLineApplication application,
        string commandLine
    )
    {
        var exitCode = await application.RunAsync(
            ["[suggest]", commandLine],
            new Dictionary<string, string>()
        );
        exitCode.Should().Be(0);

        var output = FakeConsole.ReadOutputString();
        var newOutput = output[_readOutputLength..];
        _readOutputLength = output.Length;

        // Shells expect LF, regardless of the platform
        newOutput.Should().NotContain("\r");

        return newOutput
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split('\t'))
            .ToArray();
    }

    [Fact]
    public async Task I_can_get_command_name_suggestions()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var suggestions = await SuggestAsync(application, "app ");

        // Assert
        suggestions.Select(s => s[0]).Should().Equal("device", "devices", "flaky", "login");

        suggestions.Single(s => s[0] == "device")[2].Should().Be("Shows a device.");
    }

    [Fact]
    public async Task I_can_get_command_name_suggestions_filtered_by_what_I_have_typed()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var suggestions = await SuggestAsync(application, "app DEV");

        // Assert
        suggestions.Select(s => s[0]).Should().Equal("device", "devices");
    }

    [Fact]
    public async Task I_can_get_suggestions_for_the_next_word_of_a_multi_word_command_name()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var suggestions = await SuggestAsync(application, "app device ");

        // Assert
        // The subcommand, followed by values of the parameter of the "device" command itself
        suggestions.Select(s => s[0]).Should().Equal("reboot", "id-1", "id-2", "id-3");
    }

    [Fact]
    public async Task I_can_get_option_name_suggestions()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var longSuggestions = await SuggestAsync(application, "app device id-1 --");
        var allSuggestions = await SuggestAsync(application, "app device id-1 -");

        // Assert
        longSuggestions
            .Select(s => s[0])
            .Should()
            .BeEquivalentTo("--verbose", "--color", "--tag", "--help");

        allSuggestions
            .Select(s => s[0])
            .Should()
            .BeEquivalentTo("--verbose", "--color", "--tag", "--help", "-v", "-c", "-t", "-h");

        longSuggestions.Single(s => s[0] == "--verbose")[2].Should().Be("Prints more details.");
    }

    [Fact]
    public async Task I_can_get_option_name_suggestions_that_exclude_options_I_have_already_used()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var suggestions = await SuggestAsync(application, "app device id-1 --verbose -");

        // Assert
        suggestions.Select(s => s[0]).Should().NotContain(["--verbose", "-v"]);
        suggestions.Select(s => s[0]).Should().Contain(["--color", "-c"]);
    }

    [Fact]
    public async Task I_can_get_enum_value_suggestions_for_an_option()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var all = await SuggestAsync(application, "app device id-1 --color ");
        var filtered = await SuggestAsync(application, "app device id-1 -c g");

        // Assert
        all.Select(s => s[0]).Should().Equal("Red", "Green", "Blue");
        filtered.Select(s => s[0]).Should().Equal("Green");
    }

    [Fact]
    public async Task I_can_get_option_name_suggestions_after_an_option_that_has_everything_it_needs()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var suggestions = await SuggestAsync(application, "app device id-1 --color Red ");

        // Assert
        suggestions.Select(s => s[0]).Should().Contain("--verbose").And.NotContain("--color");
    }

    [Fact]
    public async Task I_can_get_suggestions_from_a_completion_provider()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var suggestions = await SuggestAsync(application, "app device phone");

        // Assert
        suggestions.Should().HaveCount(2);

        // Value, display text, description
        suggestions[0].Should().Equal("id-1", "Phone One", "First phone");
        suggestions[1].Should().Equal("id-2", "Phone Two", "");
    }

    [Fact]
    public async Task I_can_get_suggestions_from_a_completion_provider_on_a_subcommand()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var suggestions = await SuggestAsync(application, "app device reboot lap");

        // Assert
        suggestions.Select(s => s[0]).Should().Equal("id-3");
    }

    [Fact]
    public async Task Completion_providers_receive_information_about_what_is_being_completed()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var parameter = await SuggestAsync(application, "app devices ab");
        var option = await SuggestAsync(application, "app device id-1 -v --tag one two th");

        // Assert
        parameter.Single()[0].Should().Be("devices|Mode|ab|devices");
        option.Single()[0].Should().Be("device|Tags|th|device,id-1,-v,--tag,one,two");
    }

    [Fact]
    public async Task Completion_providers_receive_quoted_words_without_quotes()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var suggestions = await SuggestAsync(application, "app devices \"two words");

        // Assert
        suggestions.Single()[0].Should().Be("devices|Mode|two words|devices");
    }

    [Fact]
    public async Task I_get_no_suggestions_for_inputs_that_have_already_been_provided()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var suggestions = await SuggestAsync(application, "app device id-1 ");

        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task I_get_suggestions_with_separators_stripped_from_their_text()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var suggestions = await SuggestAsync(application, "app device laptop");

        // Assert
        suggestions.Should().ContainSingle().Which.Should().HaveCount(3);
        suggestions[0][2].Should().Be("Third device  with a messy description");
    }

    [Fact]
    public async Task I_get_no_suggestions_if_the_completion_provider_fails()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var suggestions = await SuggestAsync(application, "app flaky ");

        // Assert
        suggestions.Should().BeEmpty();
        FakeConsole.ReadErrorString().Should().BeEmpty();
    }

    [Fact]
    public async Task I_get_no_suggestions_if_the_completion_provider_takes_too_long()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion(TimeSpan.FromMilliseconds(200)).Build();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var suggestions = await SuggestAsync(application, "app flaky x --slow ");

        // Assert
        suggestions.Should().BeEmpty();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Completion_providers_are_created_using_the_configured_type_instantiator()
    {
        // Arrange
        var requestedTypes = new List<Type>();

        var application = CreateBuilder()
            .AllowCompletion()
            .UseTypeInstantiator(t =>
            {
                requestedTypes.Add(t);
                return Activator.CreateInstance(t)!;
            })
            .Build();

        // Act
        await SuggestAsync(application, "app device ");

        // Assert
        requestedTypes.Select(t => t.Name).Should().Equal("DeviceProvider");
    }

    [Fact]
    public async Task Suggestions_are_not_provided_unless_completion_is_enabled()
    {
        // Arrange
        var application = CreateBuilder().Build();

        // Act
        var exitCode = await application.RunAsync(
            ["[suggest]", "app "],
            new Dictionary<string, string>()
        );

        // Assert
        exitCode.Should().NotBe(0);
        FakeConsole.ReadErrorString().Should().Contain("Unrecognized parameter");
    }

    [Theory]
    [InlineData("powershell", "Register-ArgumentCompleter")]
    [InlineData("pwsh", "Register-ArgumentCompleter")]
    [InlineData("bash", "complete -F")]
    [InlineData("zsh", "compdef")]
    public async Task I_can_get_a_shell_integration_script(string shell, string expectedFragment)
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var exitCode = await application.RunAsync(
            ["[completion]", shell],
            new Dictionary<string, string>()
        );

        // Assert
        exitCode.Should().Be(0);

        var script = FakeConsole.ReadOutputString();
        script.Should().Contain(expectedFragment).And.Contain("app").And.Contain("[suggest]");
        script.Should().NotContain("__NAME__").And.NotContain("__FUNCTION__");
        script.Should().NotContain("\r");
    }

    [Fact]
    public async Task I_cannot_get_a_shell_integration_script_for_an_unsupported_shell()
    {
        // Arrange
        var application = CreateBuilder().AllowCompletion().Build();

        // Act
        var exitCode = await application.RunAsync(
            ["[completion]", "cmd"],
            new Dictionary<string, string>()
        );

        // Assert
        exitCode.Should().NotBe(0);
        FakeConsole.ReadErrorString().Should().Contain("Unsupported shell 'cmd'");
    }

    [Fact]
    public void I_cannot_specify_a_completion_provider_that_does_not_implement_the_interface()
    {
        // Act & assert
        var act = () =>
            CommandCompiler.Compile(
                // lang=csharp
                """
                public class NotAProvider;

                [Command]
                public partial class TestCommand : ICommand
                {
                    [CommandParameter(0, CompletionProvider = typeof(NotAProvider))]
                    public required string Foo { get; set; }

                    public ValueTask ExecuteAsync(IConsole console) => default;
                }
                """
            );

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                $"*{DiagnosticDescriptors.CommandInputCompletionProviderMustImplementInterface.Id}*"
            );
    }
}

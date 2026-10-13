using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CliFx.Activation;

namespace CliFx.Binding;

/// <summary>
/// Describes a binding between a CLR property and a parameter input of a command.
/// </summary>
public class CommandParameterDescriptor(
    PropertyDescriptor property,
    int order,
    string name,
    bool isRequired,
    string? description,
    IInputConverter converter,
    IReadOnlyList<IInputValidator> validators,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type? completionProviderType = null
)
    : CommandInputDescriptor(
        property,
        isRequired,
        description,
        converter,
        validators,
        completionProviderType
    )
{
    /// <inheritdoc cref="CommandParameterAttribute.Order" />
    public int Order { get; } = order;

    /// <inheritdoc cref="CommandParameterAttribute.Name" />
    public string Name { get; } = name;

    internal string ToString(bool includeKind)
    {
        var buffer = new StringBuilder();

        if (includeKind)
            buffer.Append("Parameter ");

        if (!IsRequired)
            buffer.Append('[');

        if (IsSequenceBased)
            buffer.Append('<').Append(Name).Append("...>");
        else
            buffer.Append('<').Append(Name).Append('>');

        if (!IsRequired)
            buffer.Append(']');

        return buffer.ToString();
    }

    /// <inheritdoc />
    public override string ToString() => ToString(true);
}

/// <inheritdoc cref="CommandParameterDescriptor" />
/// <remarks>
/// Generic version used by source-generated code for static type references and AOT compatibility.
/// </remarks>
public class CommandParameterDescriptor<TCommand, TProperty>(
    PropertyDescriptor<TCommand, TProperty> property,
    int order,
    string name,
    bool isRequired,
    string? description,
    IInputConverter<TProperty> converter,
    IReadOnlyList<IInputValidator<TProperty>> validators,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type? completionProviderType = null
)
    : CommandParameterDescriptor(
        property,
        order,
        name,
        isRequired,
        description,
        converter,
        validators,
        completionProviderType
    )
    where TCommand : ICommand;

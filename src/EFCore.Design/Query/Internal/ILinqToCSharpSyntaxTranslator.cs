// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.EntityFrameworkCore.Query.Internal;

/// <summary>
///     Translates a LINQ expression tree to a Roslyn syntax tree.
/// </summary>
/// <remarks>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </remarks>
public interface ILinqToCSharpSyntaxTranslator
{
    /// <summary>
    ///     Translates a node representing a statement into a Roslyn syntax tree.
    /// </summary>
    /// <param name="node">The node to be translated.</param>
    /// <param name="collectedNamespaces">Any namespaces required by the translated code will be added to this set.</param>
    /// <returns>A Roslyn syntax tree representation of <paramref name="node" />.</returns>
    /// <remarks>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </remarks>
    SyntaxNode TranslateStatement(Expression node, ISet<string> collectedNamespaces);

    /// <summary>
    ///     Translates a node representing an expression into a Roslyn syntax tree.
    /// </summary>
    /// <param name="node">The node to be translated.</param>
    /// <param name="knownInstances">Collection of translations for statically known instances.</param>
    /// <param name="replacementMethods">Collection of translations for non-public member accesses.</param>
    /// <param name="collectedNamespaces">Any namespaces required by the translated code will be added to this set.</param>
    /// <param name="isStatement">A value indicating whether the translation can only be used in a statement context.</param>
    /// <returns>A Roslyn syntax tree representation of <paramref name="node" />.</returns>
    /// <remarks>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </remarks>
    SyntaxNode TranslateExpression(
        Expression node,
        Dictionary<object, ExpressionSyntax>? knownInstances,
        Dictionary<(MemberInfo, bool), ExpressionSyntax>? replacementMethods,
        ISet<string> collectedNamespaces,
        out bool isStatement);

    /// <summary>
    ///     Returns the captured variables detected in the last translation.
    /// </summary>
    /// <remarks>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </remarks>
    IReadOnlySet<ParameterExpression> CapturedVariables { get; }
}

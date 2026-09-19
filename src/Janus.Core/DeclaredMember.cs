using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Janus.Core;

/// <summary>
/// Reads the name of the property or field a declaration references.
/// </summary>
/// <remarks>
/// AUTHZ-MODEL-001 AC3: the declaration references a property, and this is where the
/// reference becomes the name the renderers use. CONV-CODE-004 permits the reflection
/// here and nowhere else in the shipped code.
/// </remarks>
internal static class DeclaredMember
{
    /// <summary>
    /// The name of the member the expression references.
    /// </summary>
    /// <typeparam name="TDeclared">The type the expression reads.</typeparam>
    /// <param name="reference">The reference the host wrote.</param>
    /// <returns>The member's name.</returns>
    /// <exception cref="ArgumentNullException">The expression is absent.</exception>
    /// <exception cref="ArgumentException">The expression references no member.</exception>
    public static string Of<TDeclared>(Expression<Func<TDeclared, object?>> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        // The compiler inserts a boxing conversion for a value-typed member, which
        // stands between the lambda's body and the reference the host wrote.
        Expression body = reference.Body is UnaryExpression { NodeType: ExpressionType.Convert } conversion
            ? conversion.Operand
            : reference.Body;

        if (body is not MemberExpression { Member: PropertyInfo or FieldInfo } member)
        {
            throw new ArgumentException(
                "A declaration references one property or field of the type.",
                nameof(reference));
        }

        return member.Member.Name;
    }
}

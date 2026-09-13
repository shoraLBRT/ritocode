using System.Reflection;
using System.Reflection.Emit;

namespace Ritocode.Architecture.Tests;

/// <summary>
/// The members a compiled method body refers to, and the method a person wrote that the body belongs
/// to. Reflection exposes a body only as IL bytes, so this walks them.
/// </summary>
internal static class MethodBodyReferences
{
    private const BindingFlags Declared =
        BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opCode => opCode.Value);

    /// <summary>Every method and constructor declared in <paramref name="assembly"/>, nested and compiler-generated types included.</summary>
    public static IEnumerable<MethodBase> BodiesIn(Assembly assembly) =>
        assembly.GetTypes().SelectMany(BodiesIn);

    /// <summary>Every method and constructor declared on <paramref name="type"/> itself.</summary>
    public static IEnumerable<MethodBase> BodiesIn(Type type) =>
        type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared));

    /// <summary>
    /// The methods and constructors <paramref name="method"/> calls, constructs, takes a delegate to, or
    /// names in an expression tree.
    /// </summary>
    public static IEnumerable<MethodBase> Of(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();

        if (il is null)
        {
            yield break;
        }

        var typeArguments = method.DeclaringType is { IsGenericType: true } type ? type.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;

        var position = 0;

        while (position < il.Length)
        {
            var value = il[position] == 0xFE
                ? unchecked((short)(0xFE00 | il[position + 1]))
                : il[position];

            var opCode = OpCodesByValue[value];
            position += opCode.Size;

            // A method token is the operand of call, callvirt, newobj, ldftn, ldvirtftn and jmp; an
            // expression tree names its methods with ldtoken instead.
            if (opCode.OperandType is OperandType.InlineMethod or OperandType.InlineTok
                && Resolve(method.Module, BitConverter.ToInt32(il, position), typeArguments, methodArguments) is { } member)
            {
                yield return member;
            }

            position += opCode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
                _ => 4,
            };
        }
    }

    /// <summary>
    /// The type and method name as written in source. The compiler moves an async method's body into a
    /// state machine and a lambda's into a closure class; both keep the source method's name inside
    /// angle brackets, and both are nested in the type that declared it.
    /// </summary>
    public static (Type Type, string Method) SourceOf(MethodBase method)
    {
        var name = method.Name;
        var type = method.DeclaringType!;

        while (type.DeclaringType is not null && type.Name.StartsWith('<'))
        {
            // A state machine's MoveNext is named for nothing; its type is named for the method. A
            // closure class ("<>c", "<>c__DisplayClass") is named for nothing, and its methods are.
            if (!name.StartsWith('<') && !type.Name.StartsWith("<>", StringComparison.Ordinal))
            {
                name = type.Name;
            }

            type = type.DeclaringType;
        }

        return (type, Unmangle(name));
    }

    private static MethodBase? Resolve(Module module, int token, Type[]? typeArguments, Type[]? methodArguments)
    {
        try
        {
            return module.ResolveMember(token, typeArguments, methodArguments) as MethodBase;
        }
        catch (ArgumentException)
        {
            // A token this module cannot resolve in the generic context given names nothing the rules
            // inspect: every guarded member is reached through a closed, resolvable reference.
            return null;
        }
    }

    /// <summary><c>&lt;&lt;OpenAsync&gt;b__4_0&gt;d</c> becomes <c>OpenAsync</c>.</summary>
    private static string Unmangle(string name)
    {
        while (name.StartsWith('<'))
        {
            var depth = 0;
            var close = -1;

            for (var index = 0; index < name.Length && close < 0; index++)
            {
                if (name[index] == '<')
                {
                    depth++;
                }
                else if (name[index] == '>' && --depth == 0)
                {
                    close = index;
                }
            }

            if (close <= 1)
            {
                return name;
            }

            name = name[1..close];
        }

        return name;
    }
}

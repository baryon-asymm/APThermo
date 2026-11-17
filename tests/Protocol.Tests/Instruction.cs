using System.Reflection;
using System.Reflection.Emit;

namespace APThermo.Protocol.Tests;

/// <summary>One instruction of a method body: its opcode, resolved member and literal string when present.</summary>
internal readonly record struct Instruction(OpCode Code, MemberInfo? Operand)
{
    /// <summary>A string literal resolved from an InlineString token, when present.</summary>
    public string? Literal { get; init; }
}

using System.Reflection;
using System.Reflection.Emit;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>One instruction of a method body: the opcode and the member its token names, when it names one.</summary>
internal readonly record struct Instruction(OpCode Code, MemberInfo? Operand);

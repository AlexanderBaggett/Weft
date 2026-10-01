using System.Collections.Immutable;
using Weft.Compiler.Text;

namespace Weft.Compiler.Semantics;

public enum TypeKind
{
    Void, Bool, Int8, UInt8, Int16, UInt16, Int32, UInt32, Int64, UInt64,
    Char, Float32, Float64, Decimal, Decimal128, String,
    Nominal, GenericParameter, Array, Nullable, Error
}
public sealed record WeftType(TypeKind Kind, string Name, ImmutableArray<WeftType> Arguments = default)
{
    public static readonly WeftType Void = new(TypeKind.Void, "void");
    public static readonly WeftType Bool = new(TypeKind.Bool, "bool");
    public static readonly WeftType Int8 = new(TypeKind.Int8, "int8");
    public static readonly WeftType UInt8 = new(TypeKind.UInt8, "uint8");
    public static readonly WeftType Int16 = new(TypeKind.Int16, "int16");
    public static readonly WeftType UInt16 = new(TypeKind.UInt16, "uint16");
    public static readonly WeftType Int32 = new(TypeKind.Int32, "int32");
    public static readonly WeftType UInt32 = new(TypeKind.UInt32, "uint32");
    public static readonly WeftType Int64 = new(TypeKind.Int64, "int64");
    public static readonly WeftType UInt64 = new(TypeKind.UInt64, "uint64");
    public static readonly WeftType Char = new(TypeKind.Char, "char");
    public static readonly WeftType Float32 = new(TypeKind.Float32, "float32");
    public static readonly WeftType Float64 = new(TypeKind.Float64, "float64");
    public static readonly WeftType Decimal = new(TypeKind.Decimal, "decimal");
    public static readonly WeftType Decimal128 = new(TypeKind.Decimal128, "decimal128");
    public static readonly WeftType String = new(TypeKind.String, "string");
    public static readonly WeftType Error = new(TypeKind.Error, "<error>");
    public static WeftType? Builtin(string name) => name switch
    {
        "void" => Void, "bool" => Bool,
        "sbyte" or "int8" => Int8, "byte" or "uint8" => UInt8,
        "short" or "int16" => Int16, "ushort" or "uint16" => UInt16,
        "int" or "int32" => Int32, "uint" or "uint32" => UInt32,
        "long" or "int64" => Int64, "ulong" or "uint64" => UInt64,
        "char" => Char, "float" or "float32" => Float32, "double" or "float64" => Float64,
        "decimal" => Decimal, "decimal128" => Decimal128, "string" => String, _ => null
    };
}
public enum Visibility { Private, Internal, Public }
public sealed record ConstantValue(object Value, WeftType Type);
public sealed record VariableSymbol(int Id, string Name, WeftType Type, SourceLocation Location, ConstantValue? Default = null);
public sealed record FunctionSymbol(int Id, string Name, WeftType ReturnType, ImmutableArray<VariableSymbol> Parameters,
    SourceLocation Location, Visibility Visibility = Visibility.Internal, string? ContainingType = null, VariableSymbol? Receiver = null, bool IsConstructor = false);
public sealed record FieldSymbol(int Id, string Name, WeftType Owner, WeftType Type, SourceLocation Location,
    Visibility Visibility = Visibility.Private, bool ReadOnly = false);
public sealed record PropertySymbol(string Name, WeftType Owner, WeftType Type, SourceLocation Location,
    Visibility Visibility, FunctionSymbol? Getter, FunctionSymbol? Setter, FieldSymbol? BackingField);
public sealed record TypeSymbol(string Name, SourceLocation Location, Visibility Visibility = Visibility.Internal, bool IsStatic = false);

public sealed class SymbolScope(SymbolScope? parent = null)
{
    private readonly Dictionary<string, VariableSymbol> variables = new(StringComparer.Ordinal);
    public bool Declare(VariableSymbol symbol) => variables.TryAdd(symbol.Name, symbol);
    public VariableSymbol? Lookup(string name) => variables.GetValueOrDefault(name) ?? parent?.Lookup(name);
}

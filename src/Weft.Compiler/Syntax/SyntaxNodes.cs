using System.Collections.Immutable;
using Weft.Compiler.Diagnostics;
using Weft.Compiler.Text;

namespace Weft.Compiler.Syntax;

public sealed record SyntaxTree(SourceText Source, ImmutableArray<DeclarationSyntax> Declarations, ImmutableArray<Diagnostic> Diagnostics);
public abstract record SyntaxNode(SourceLocation Location);
public sealed record TypeSyntax(string Name, ImmutableArray<TypeSyntax> Arguments, int ArrayRank, bool Nullable, SourceLocation Location) : SyntaxNode(Location);
public abstract record DeclarationSyntax(string Name, SourceLocation Location) : SyntaxNode(Location);
public sealed record NamespaceSyntax(string Namespace, bool FileScoped, ImmutableArray<DeclarationSyntax> Members, SourceLocation Location) : DeclarationSyntax(Namespace, Location);
public sealed record ParameterSyntax(TypeSyntax Type, string Name, SourceLocation Location, ExpressionSyntax? Default = null) : SyntaxNode(Location);
public sealed record ClassSyntax(string ClassName, ImmutableArray<DeclarationSyntax> Members,
    ImmutableArray<string> Modifiers, SourceLocation Location) : DeclarationSyntax(ClassName, Location);
public sealed record FunctionSyntax(string FunctionName, TypeSyntax ReturnType, ImmutableArray<ParameterSyntax> Parameters,
    BlockSyntax? Body, ImmutableArray<string> Modifiers, SourceLocation Location, bool IsConstructor = false) : DeclarationSyntax(FunctionName, Location);
public sealed record FieldSyntax(string FieldName, TypeSyntax Type, ExpressionSyntax? Initializer,
    ImmutableArray<string> Modifiers, SourceLocation Location) : DeclarationSyntax(FieldName, Location);
public sealed record PropertySyntax(string PropertyName, TypeSyntax Type, GroupElement Body,
    ImmutableArray<string> Modifiers, SourceLocation Location) : DeclarationSyntax(PropertyName, Location);

public enum ConstructKind { Origin, Model, Class, Record, Interface, Service, Receiver, Sink, Transform, Validate, Filter, Rule, Trigger, Ruleset, Suppress, Middleware, Pipeline, Ambient, Topic, Channel, Table, Flag, Canary, Kill, SwitchGroup, Scope, Use, Cache, Type, Extern, Outcome }
// Lossless token trees retain nested code, match blocks, clauses, and source locations.
// Dedicated feature binders specialize these nodes in the phases that implement them.
public abstract record SyntaxElement(SourceLocation Location) : SyntaxNode(Location);
public sealed record TokenElement(SyntaxToken Token) : SyntaxElement(Token.Location);
public sealed record GroupElement(string Delimiter, ImmutableArray<SyntaxElement> Elements, SourceLocation Location) : SyntaxElement(Location);
public sealed record ConstructSyntax(ConstructKind Kind, string ConstructName, ImmutableArray<string> Modifiers,
    ImmutableArray<SyntaxElement> Header, GroupElement? Body, SourceLocation Location) : DeclarationSyntax(ConstructName, Location);

public abstract record StatementSyntax(SourceLocation Location) : SyntaxNode(Location);
public sealed record BlockSyntax(ImmutableArray<StatementSyntax> Statements, SourceLocation Location) : StatementSyntax(Location);
public sealed record VariableSyntax(TypeSyntax? Type, string Name, ExpressionSyntax Initializer, SourceLocation Location) : StatementSyntax(Location);
public sealed record ReturnSyntax(ExpressionSyntax? Expression, SourceLocation Location) : StatementSyntax(Location);
public sealed record ExpressionStatementSyntax(ExpressionSyntax Expression, SourceLocation Location) : StatementSyntax(Location);
public sealed record IfSyntax(ExpressionSyntax Condition, StatementSyntax Then, StatementSyntax? Else, SourceLocation Location) : StatementSyntax(Location);
public sealed record WhileSyntax(ExpressionSyntax Condition, StatementSyntax Body, SourceLocation Location) : StatementSyntax(Location);
public sealed record DoWhileSyntax(StatementSyntax Body, ExpressionSyntax Condition, SourceLocation Location) : StatementSyntax(Location);
public sealed record ForSyntax(ImmutableArray<StatementSyntax> Initializers, ExpressionSyntax? Condition,
    ImmutableArray<ExpressionSyntax> Iterators, StatementSyntax Body, SourceLocation Location) : StatementSyntax(Location);
public sealed record BreakSyntax(SourceLocation Location) : StatementSyntax(Location);
public sealed record ContinueSyntax(SourceLocation Location) : StatementSyntax(Location);
public sealed record EmptySyntax(SourceLocation Location) : StatementSyntax(Location);
public sealed record EffectScopeSyntax(string Kind, ImmutableArray<SyntaxElement> Header, GroupElement Body, SourceLocation Location) : StatementSyntax(Location);

public abstract record ExpressionSyntax(SourceLocation Location) : SyntaxNode(Location);
public sealed record LiteralSyntax(SyntaxToken Token) : ExpressionSyntax(Token.Location);
public sealed record NameSyntax(string Name, SourceLocation Location) : ExpressionSyntax(Location);
public sealed record UnarySyntax(string Operator, ExpressionSyntax Operand, SourceLocation Location) : ExpressionSyntax(Location);
public sealed record UpdateSyntax(string Operator, ExpressionSyntax Operand, bool Postfix, SourceLocation Location) : ExpressionSyntax(Location);
public sealed record BinarySyntax(ExpressionSyntax Left, string Operator, ExpressionSyntax Right, SourceLocation Location) : ExpressionSyntax(Location);
public sealed record ConditionalSyntax(ExpressionSyntax Condition, ExpressionSyntax WhenTrue, ExpressionSyntax WhenFalse,
    SourceLocation Location) : ExpressionSyntax(Location);
public sealed record ArgumentSyntax(ExpressionSyntax Expression, string? Name, SourceLocation Location) : SyntaxNode(Location);
public sealed record CallSyntax(ExpressionSyntax Target, ImmutableArray<ArgumentSyntax> Arguments, SourceLocation Location) : ExpressionSyntax(Location);
public sealed record NewSyntax(TypeSyntax Type, ImmutableArray<ArgumentSyntax> Arguments, SourceLocation Location) : ExpressionSyntax(Location);
public sealed record MemberSyntax(ExpressionSyntax Target, string Member, SourceLocation Location) : ExpressionSyntax(Location);

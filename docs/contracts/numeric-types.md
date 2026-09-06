# Numeric types on .NET and JVM

The designer's 2026-09-06 direction is to support the common C# and Java numeric types
and follow C# behavior wherever possible. A missing host primitive calls for runtime
support, not removal of a language type. This supersedes the four-type limit in the
original compiler design. These are full-release contracts; most operations are
Phase 2 work, not implemented by adding their names to the foundation type model.

## Common types and mappings

Weft uses C# spellings plus the explicit-width spellings already used in the design.
The byte names follow C#: `byte` is unsigned and `sbyte` is signed. A Java binding maps
Java `byte` to Weft `sbyte`; binary payload adapters preserve all eight bits explicitly.

| Weft spelling / alias | Meaning | .NET implementation | JVM implementation |
|---|---|---|---|
| `sbyte` / `int8` | Signed 8-bit integer | `sbyte` | `byte` |
| `byte` / `uint8` | Unsigned 8-bit integer | `byte` | Widened storage plus range/conversion helpers |
| `short` / `int16` | Signed 16-bit integer | `short` | `short` |
| `ushort` / `uint16` | Unsigned 16-bit integer | `ushort` | Widened storage plus range/conversion helpers |
| `int` / `int32` | Signed 32-bit integer | `int` | `int` |
| `uint` / `uint32` | Unsigned 32-bit integer | `uint` | Bit-pattern storage plus unsigned operations |
| `long` / `int64` | Signed 64-bit integer | `long` | `long` |
| `ulong` / `uint64` | Unsigned 64-bit integer | `ulong` | Bit-pattern storage plus unsigned operations |
| `char` | One UTF-16 code unit | `char` | `char` |
| `float` / `float32` | Binary32 floating point | `float` | `float` |
| `double` / `float64` | Binary64 floating point | `double` | `double` |
| `decimal` | C# decimal behavior | `System.Decimal` | Weft value wrapper using `BigDecimal` |
| `BigInteger` | Arbitrary-precision signed integer, library type | `System.Numerics.BigInteger` | `java.math.BigInteger` |
| `BigDecimal` | Arbitrary-precision decimal, library type | Weft library over integer coefficient and scale | Weft API over `java.math.BigDecimal` |
| `decimal128` | Existing finite 34-digit decimal contract | Weft runtime type | Weft runtime type |

This mapping follows the host inventories documented by
[Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types)
and the [Java language specification](https://docs.oracle.com/javase/specs/jls/se21/html/jls-4.html#jls-4.2).
Java's arbitrary-precision types are documented in
[java.math](https://docs.oracle.com/en/java/javase/21/docs/api/java.base/java/math/package-summary.html).
Architecture-sized integers and specialized host numeric APIs remain available through
paired library bindings with explicit width/representation contracts; they must not
silently change an ordinary Weft integer's width between backends.

## Ordinary evaluation and arithmetic

Receivers and arguments evaluate left to right, once, including calls lowered through
runtime helpers. Use C# numeric promotions, implicit conversions, constant-expression
conversions, suffixes, casts, and checked/unchecked rules. `byte + byte`, for example,
produces `int`; assignment back to a byte needs a permitted conversion. Unsigned
comparisons, division, shifts, parsing, and formatting must preserve the unsigned
value on the JVM. Conversion and arithmetic behavior cannot depend on storage choice.

Runtime integer addition, subtraction, multiplication, and negation wrap by default;
checked contexts throw on overflow. Constant-expression overflow is diagnosed by
default. Minimum signed int/long divided by -1 throws on both backends. A zero divisor
throws; remainder of minimum signed int/long by -1 is zero. This replaces the old
bootstrap division-wrap policy. See [C# arithmetic behavior](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/arithmetic-operators).

Binary floating types retain their signed zero, infinity, NaN, rounding, and comparison
behavior. Do not permit a backend optimization to change specified evaluation or
reassociate expressions. Decimal/binary-float mixing requires an explicit conversion,
as in C#. Portable formatting and serialization are culture-independent unless the
caller supplies a culture; raw hash values are not a cross-platform observation.

## C# decimal support

`decimal` is a distinct value type, copied as an ordinary numeric value even when the
JVM uses an immutable object. Its magnitude is limited by a 96-bit coefficient and a
scale from 0 to 28. It has approximately 28–29 significant digits, not decimal128's 34.
[C# decimal reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types)

The JVM wrapper must match C# arithmetic, rounding, overflow, division-by-zero,
conversions, comparison, hashing consistency, scale-sensitive formatting, and parsing.
Using a fixed `MathContext(29)` is insufficient: the coefficient bound and operation
rules also matter. Numeric equality treats `1.0` and `1.00` alike. Java's native
`BigDecimal.equals` compares their scales as well, so Weft must supply its own numeric
equality and consistent hashing. [BigDecimal comparison contract](https://docs.oracle.com/en/java/javase/25/docs/api/java.base/java/math/BigDecimal.html#equals(java.lang.Object))

Before implementing the complete operations, add independent expected cases for
`0.1m + 0.2m`, `1m / 3m`, halfway rounding, the maximum coefficient, scale 28,
underflow, overflow, zero division, casts, and equal values with different scales.
Test generated Weft programs on each backend against those expectations. Target the
behavior under implementation; no hosted CI or new release gates are needed now.

`BigDecimal` is a separate library API for arbitrary precision. Its precision and
rounding are explicit at operations that need them; an exact nonterminating division
fails unless a rounding context is supplied. A JVM `BigDecimal` is never silently
accepted as a bounded `decimal` without conversion checks.

## Existing decimal128 and Money

Retain the extended finite profile and `Money` currency behavior in
[decision 0002, section B](../decisions/0002-ordinary-and-portable-contracts.md#b-numeric-support-decimal128-and-money).
That type does not become an alias for C# `decimal`, and implementing `decimal` does
not complete `decimal128`. The [decimal128 vectors](../acceptance/decimal-vectors.json)
retain independent expected outcomes for its wider precision and exponent range.

## Implementation ownership

P02-008 owns all numeric operations and conversions above. P02-032 owns useful
cross-backend behavior cases; serialization and external-binding tasks cover their
numeric boundaries. Until a type's execution is implemented, the compiler reports
WF2009 rather than pretending the host's closest type has identical behavior.

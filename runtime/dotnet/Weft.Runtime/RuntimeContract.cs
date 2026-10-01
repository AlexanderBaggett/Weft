using System.Globalization;

namespace Weft.Runtime;

public static class RuntimeContract
{
    public const string Abi = "1";
    public static void RequireAbi(string expected)
    {
        if (expected != Abi) throw new InvalidOperationException($"Weft runtime ABI {Abi} cannot execute ABI {expected}.");
    }
    public static T ReadStatic<T>(T? value, string field) where T : class => value
        ?? throw new InvalidOperationException($"Static field '{field}' was read before initialization.");
    public static void WriteLine(string value) => Console.WriteLine(value);
    public static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    public static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);
    public static string Text(bool value) => value ? "true" : "false";
    // Follow C#: signed minimum divided by -1 overflows even in an unchecked context.
    public static int Divide(int left, int right) => left / right;
    public static long Divide(long left, long right) => left / right;
    public static int Remainder(int left, int right) => left == int.MinValue && right == -1 ? 0 : left % right;
    public static long Remainder(long left, long right) => left == long.MinValue && right == -1 ? 0 : left % right;
}

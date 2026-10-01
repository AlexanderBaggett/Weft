package weft.runtime;

/** Portable bootstrap runtime. Host types never enter the compiler IR. */
public final class RuntimeContract {
    private RuntimeContract() { }
    public static final String ABI = "1";
    public static void requireAbi(String expected) {
        if (!ABI.equals(expected)) throw new IllegalStateException("Weft runtime ABI " + ABI + " cannot execute ABI " + expected);
    }
    public static <T> T readStatic(T value, String field) {
        if (value == null) throw new IllegalStateException("Static field '" + field + "' was read before initialization.");
        return value;
    }
    public static void writeLine(String value) { System.out.println(value); }
    public static String text(int value) { return Integer.toString(value); }
    public static String text(long value) { return Long.toString(value); }
    public static String text(boolean value) { return Boolean.toString(value); }
    public static int divide(int left, int right) {
        // Java wraps this case; the Weft contract follows C# division overflow.
        if (left == Integer.MIN_VALUE && right == -1) throw new ArithmeticException("Integer division overflow.");
        return left / right;
    }
    public static long divide(long left, long right) {
        if (left == Long.MIN_VALUE && right == -1) throw new ArithmeticException("Integer division overflow.");
        return left / right;
    }
    public static int remainder(int left, int right) { return left % right; }
    public static long remainder(long left, long right) { return left % right; }
}

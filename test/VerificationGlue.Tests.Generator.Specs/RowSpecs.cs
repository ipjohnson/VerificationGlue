namespace VerificationGlue.Tests.Generator.Specs;

/// <summary>
/// <c>[Row]</c> registers one test per row. A row's ID comes from its typed values, so inserting or
/// reordering rows keeps each row's ID, and identical rows get an occurrence suffix.
/// </summary>
public class RowSpecs
{
    private const string Method = "Shop.OrderTests.Totals(System.Int32,System.Int64,System.String,VerificationGlue.Tests.TestContext)";

    private static string Source(params string[] rows) => $$"""
        using VerificationGlue.Tests;
        using static VerificationGlue.VerifySyntax;

        namespace Shop;

        public partial class OrderTests
        {
            [VerifyTest]
            {{string.Join("\n    ", rows)}}
            public void Totals(int qty, long price, string? note, TestContext context) =>
                Verify(context.DisplayName == $"Totals(qty: {qty}, price: {price}, note: {(note is null ? "null" : "\"" + note + "\"")})");
        }
        """;

    [Fact]
    public async Task Each_row_is_a_test_that_gets_its_own_values()
    {
        var tests = Registrations.Load(Source("[Row(2, 10, \"gift\")]", "[Row(3, 5, null)]"), rewrite: false, new TestGenerator());

        Assert.Equal(
            [
                Method + "#row(System.Int32:2,System.Int64:10,System.String:\"gift\")",
                Method + "#row(System.Int32:3,System.Int64:5,null)",
            ],
            tests.Keys);
        foreach (var test in tests.Values)
        {
            Assert.Empty(await test());
        }
    }

    [Fact]
    public void Inserted_and_reordered_rows_keep_their_ids()
    {
        var before = Registrations.Names(Source("[Row(2, 10, \"gift\")]", "[Row(3, 5, null)]"));
        var after = Registrations.Names(Source("[Row(3, 5, null)]", "[Row(1, 1, \"new\")]", "[Row(2, 10, \"gift\")]"));

        Assert.Equal(3, after.Length);
        Assert.All(before, row => Assert.Contains(row, after));
    }

    [Fact]
    public void Identical_rows_get_an_occurrence_suffix()
    {
        var names = Registrations.Names(Source("[Row(2, 10, null)]", "[Row(3, 5, null)]", "[Row(2, 10, null)]"));

        var row = Method + "#row(System.Int32:2,System.Int64:10,null)";
        Assert.Equal([row, Method + "#row(System.Int32:3,System.Int64:5,null)", row + "#2"], names.Select(name => name.Uid));
        Assert.Equal("Totals(qty: 2, price: 10, note: null)", names[2].DisplayName);
    }

    [Fact]
    public void A_row_value_is_converted_to_its_parameter_type_before_the_id_is_computed()
    {
        var spelled = Registrations.Names(Source("[Row((short)2, 10, null)]"));
        var plain = Registrations.Names(Source("[Row(2, 10L, null)]"));

        Assert.Equal(plain, spelled);
    }

    [Fact]
    public async Task Whole_numbers_and_chars_are_converted_to_decimal_nint_and_nuint_parameters()
    {
        var tests = Registrations.Load("""
            using VerificationGlue.Tests;
            using static VerificationGlue.VerifySyntax;

            namespace Shop;

            public partial class OrderTests
            {
                [VerifyTest]
                [Row(2, 10, 'a', -3, 4U)]
                [Row(2, 10L, null, (short)-3, 4)]
                public void Totals(int qty, decimal price, decimal? tax, nint offset, nuint size) =>
                    Verify(qty * price == 20m && (tax is null || tax == 97m) && offset == -3 && size == 4);
            }
            """, rewrite: false, new TestGenerator());

        const string Method = "Shop.OrderTests.Totals(System.Int32,System.Decimal,System.Nullable<System.Decimal>,System.IntPtr,System.UIntPtr)";
        Assert.Equal(
            [
                Method + "#row(System.Int32:2,System.Decimal:10,System.Decimal:97,System.IntPtr:-3,System.UIntPtr:4)",
                Method + "#row(System.Int32:2,System.Decimal:10,null,System.IntPtr:-3,System.UIntPtr:4)",
            ],
            tests.Keys);
        foreach (var test in tests.Values)
        {
            Assert.Empty(await test());
        }
    }

    [Fact]
    public void The_default_name_lists_each_row_value_with_its_parameter_name()
    {
        var names = Registrations.Names("""
            using VerificationGlue.Tests;

            namespace Shop;

            public enum Level { Low, High }

            public partial class OrderTests
            {
                [VerifyTest]
                public void Plain() { }

                [VerifyTest]
                [Row("a\"b", 'c', true, Level.High, 1.5, null, typeof(int), new[] { 1, 2 })]
                public void Values(string text, char letter, bool flag, Level level, double rate, string? note, Type type, int[] items, CancellationToken token) { }
            }
            """);

        Assert.Equal(
            ["Plain", "Values(text: \"a\\\"b\", letter: 'c', flag: true, level: Level.High, rate: 1.5, note: null, type: typeof(Int32), items: [1, 2])"],
            names.Select(name => name.DisplayName));
    }

    [Fact]
    public void The_narrowest_name_formatter_writes_the_display_name()
    {
        var names = Registrations.Names("""
            using VerificationGlue.Tests;

            [assembly: NameFormatter<Shop.Upper>]

            namespace Shop;

            public sealed class Upper : ITestNameFormatter
            {
                public static string Format(in TestNameContext context) => context.MethodName.ToUpperInvariant();
            }

            public sealed class Arrow : ITestNameFormatter
            {
                public static string Format(in TestNameContext context) =>
                    context.TypeName + " -> " + context.MethodName + " " + string.Join(" x ", context.Arguments);
            }

            public sealed class Throwing : ITestNameFormatter
            {
                public static string Format(in TestNameContext context) => throw new InvalidOperationException("The formatter failed.");
            }

            public partial class AssemblyTests
            {
                [VerifyTest]
                public void From_the_assembly() { }
            }

            [NameFormatter<Arrow>]
            public partial class ClassTests
            {
                [VerifyTest]
                [Row(2, 10)]
                public void From_the_class(int qty, int price) { }

                [VerifyTest]
                [NameFormatter<Upper>]
                public void From_the_method() { }

                [VerifyTest]
                [NameFormatter<Throwing>]
                [Row(3)]
                public void Falls_back(int qty) { }
            }
            """);

        Assert.Equal(
            ["FROM_THE_ASSEMBLY", "Shop.ClassTests -> From_the_class 2 x 10", "FROM_THE_METHOD", "Falls_back(qty: 3)"],
            names.Select(name => name.DisplayName));
    }
}

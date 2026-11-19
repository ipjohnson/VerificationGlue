using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace VerificationGlue.Generator.Specs;

public class EquivalenceSpecs
{
    private const string Path = "/repo/Project/Tests/Equivalence.cs";

    private const string Source = """
        using System;
        using System.Collections.Generic;
        using VerificationGlue;
        using static VerificationGlue.VerifySyntax;

        namespace Sample;

        public enum Status { Open, Paid }

        public sealed record Address(string City, string Street);

        public sealed record Customer(string Name, Address Address);

        public sealed record Invoice(int Id, Customer? Customer, Status Status);

        public sealed class Money : IEquatable<Money>
        {
            public Money(decimal amount, string currency) { Amount = amount; Currency = currency; }
            public decimal Amount { get; }
            public string Currency { get; }
            public bool Equals(Money? other) => other is not null && other.Amount == Amount && string.Equals(other.Currency, Currency, StringComparison.OrdinalIgnoreCase);
            public override bool Equals(object? obj) => Equals(obj as Money);
            public override int GetHashCode() => Amount.GetHashCode();
            public override string ToString() => $"{Amount} {Currency}";
        }

        public sealed class Line
        {
            public string Sku = "";
            public Money Price { get; set; } = new(0, "EUR");
        }

        public struct Point
        {
            public int X;
            public int Y;
        }

        public sealed class Shape
        {
            public Point Origin { get; set; }
            public Point? Anchor { get; set; }
        }

        public sealed class Node
        {
            public int Value { get; set; }
            public Node? Next { get; set; }
        }

        public sealed class Wide
        {
            public int A, B, C, D, E, F, G, H, I, J, K, L;
        }

        public sealed class Batch
        {
            public int[] Counts { get; set; } = [];
            public IReadOnlyList<string> Names { get; set; } = [];
            public Point?[] Marks { get; set; } = [];
            public int[][] Grid { get; set; } = [];
        }

        public sealed record Route(string Code, List<Address> Stops);

        public sealed record Stock(Dictionary<string, int> Levels, HashSet<string> Tags);

        public static partial class Cases
        {
            private static Invoice Invoice(string city, Status status = Status.Open) => new(7, new Customer("Ada", new Address(city, "High St")), status);

            public static void NestedRecords() => VerifyEquivalent(Invoice("London"), Invoice("Paris", Status.Paid));
            public static void NestedRecordsPass() => VerifyEquivalent(Invoice("London"), Invoice("London"));
            public static void NullMember() => VerifyEquivalent(new Invoice(7, null, Status.Open), Invoice("Paris"));
            public static void NullActual() => VerifyEquivalent<Invoice?>(null, Invoice("Paris"));
            public static void NullExpected() => VerifyEquivalent<Invoice?>(Invoice("Paris"), null);
            public static void BothNull() => VerifyEquivalent<Invoice?>(null, null);
            public static void EquatableMember() => VerifyEquivalent(new Line { Sku = "A", Price = new(40, "EUR") }, new Line { Sku = "B", Price = new(40, "GBP") });
            public static void EquatableMemberUsesEquals() => VerifyEquivalent(new Line { Sku = "A", Price = new(40, "eur") }, new Line { Sku = "A", Price = new(40, "EUR") });
            public static void Limit() => VerifyEquivalent(new Wide(), new Wide { A = 1, B = 1, C = 1, D = 1, E = 1, F = 1, G = 1, H = 1, I = 1, J = 1, K = 1, L = 1 });
            public static void OneMore() => VerifyEquivalent(new Wide(), new Wide { A = 1, B = 1, C = 1, D = 1, E = 1, F = 1, G = 1, H = 1, I = 1, J = 1, K = 1 });
            public static void Structs() => VerifyEquivalent(new Shape { Origin = new Point { X = 1 }, Anchor = new Point { Y = 2 } }, new Shape { Origin = new Point { X = 3 }, Anchor = new Point { Y = 4 } });
            public static void NullableStruct() => VerifyEquivalent(new Shape(), new Shape { Anchor = new Point() });
            public static void Primitive() => VerifyEquivalent(3, 4);
            public static void Private() => VerifyEquivalent(new Hidden(1), new Hidden(2));
            public static void OneItem() => VerifyEquivalent(new Batch { Counts = [1, 2, 3] }, new Batch { Counts = [1, 5, 3] });
            public static void Lengths() => VerifyEquivalent(new Batch { Counts = [1, 2], Names = ["Ann", "Bo"] }, new Batch { Counts = [1, 2, 3], Names = ["Ann", "Cy", "Di"] });
            public static void NullList() => VerifyEquivalent(new Batch { Counts = null! }, new Batch { Counts = [1] });
            public static void NullableItems() => VerifyEquivalent(new Batch { Marks = [new Point { X = 1 }, null] }, new Batch { Marks = [new Point { X = 2 }, new Point()] });
            public static void NestedArrays() => VerifyEquivalent(new Batch { Grid = [[1], [2, 3]] }, new Batch { Grid = [[1], [4, 3]] });
            public static void RecordList() => VerifyEquivalent(Route("Rue"), Route("Quai"));
            public static void RecordListPass() => VerifyEquivalent(Route("Rue"), Route("Rue"));
            public static void ListRoot() => VerifyEquivalent(new List<Address> { new("London", "High St") }, new List<Address> { new("Paris", "High St"), new("Rome", "Via") });
            public static void Dictionary() => VerifyEquivalent(new Stock(new() { ["gift"] = 3 }, ["fragile"]), new Stock(new() { ["gift"] = 3 }, ["fragile"]));
            public static void SameDictionary()
            {
                var levels = new Dictionary<string, int> { ["gift"] = 3 };
                var tags = new HashSet<string> { "fragile" };
                VerifyEquivalent(new Stock(levels, tags), new Stock(levels, tags));
            }

            public static void UnorderedPass() => VerifyEquivalent(new Batch { Counts = [1, 2, 2, 3], Names = ["Ann", "Bo"] }, new Batch { Counts = [2, 3, 1, 2], Names = ["Bo", "Ann"] }, o => o.Unordered(x => x.Counts).Unordered(x => x.Names));
            public static void UnorderedDuplicates() => VerifyEquivalent(new Batch { Counts = [1, 1, 2] }, new Batch { Counts = [1, 2, 2] }, o => o.Unordered(x => x.Counts));
            public static void UnorderedRecords() => VerifyEquivalent(Stops(Paris, London, London), Stops(London, Paris, Paris), o => o.Unordered(x => x.Stops));
            public static void UnorderedRecordsPass() => VerifyEquivalent(Stops(Paris, London, Paris), Stops(Paris, Paris, London), o => o.Unordered(x => x.Stops));
            public static void UnorderedRoot() => VerifyEquivalent(new List<Address> { London, Paris }, new List<Address> { Paris, Paris }, o => o.Unordered(x => x));
            public static void IgnoreNested() => VerifyEquivalent(Invoice("London"), Invoice("Paris", Status.Paid), o => o.Ignore(x => x.Customer!.Address.City));
            public static void IgnoreConditional() => VerifyEquivalent(Invoice("London"), Invoice("Paris"), o => o.Ignore(x => x.Customer?.Address.City));
            public static void IgnoreAll() => VerifyEquivalent(Invoice("London"), Invoice("Paris", Status.Paid), o => o.Ignore(x => x.Customer).Ignore(x => x.Status));
            public static void PrimitiveWithOptions() => VerifyEquivalent(3, 4, o => o);
            public static void UnappliedOption() => VerifyEquivalent(Invoice("London"), Invoice("Paris", Status.Paid), o => o.Ignore(x => x.Customer!.Name.Trim()).Ignore(x => x.Status));

            private static readonly Address London = new("London", "High St");
            private static readonly Address Paris = new("Paris", "Quai");

            private static Route Stops(params Address[] stops) => new("R1", [.. stops]);

            [Verify]
            public static void InCopyWithOptions() => VerifyEquivalent(Invoice("London"), Invoice("Paris", Status.Paid), o => o.Ignore(x => x.Customer!.Address.City));

            private static Route Route(string street) => new("R1", [new Address("London", "High St"), new Address("Paris", street)]);

            public static void Cycle()
            {
                var actual = new Node { Value = 1 };
                actual.Next = actual;
                var expected = new Node { Value = 1 };
                expected.Next = expected;
                VerifyEquivalent(actual, expected);
            }

            [Verify]
            public static void InCopy() => VerifyEquivalent(Invoice("London"), Invoice("Paris"));

            private sealed record Hidden(int Value);
        }
        """;

    private static readonly Lazy<Assembly> Assembly = new(() => GeneratorHarness.Load(rewrite: true, (Path, Source)));

    [VerifyTest]
    public void Nested_records_list_the_path_of_each_member_that_differs()
    {
        Assert.Equal(
            Lines(
                "VerifyEquivalent failed: Invoice(\"London\") is not equivalent to Invoice(\"Paris\", Status.Paid)",
                "  Customer.Address.City: expected \"Paris\", found \"London\"",
                "  Status: expected Paid, found Open",
                $"  at Tests/Equivalence.cs:{LineOf("NestedRecords()")}"),
            Fail("NestedRecords").Message);
    }

    [VerifyTest]
    public void Equivalent_values_pass()
    {
        Run("NestedRecordsPass");
        Run("BothNull");
        Run("EquatableMemberUsesEquals");
        Run("Cycle");
        Run("RecordListPass");
        Run("SameDictionary");
    }

    [VerifyTest]
    public void An_array_member_names_the_index_of_the_item_that_differs()
    {
        Assert.Equal(
            Lines(
                "VerifyEquivalent failed: new Batch { Counts = [1, 2, 3] } is not equivalent to new Batch { Counts = [1, 5, 3] }",
                "  Counts[1]: expected 5, found 2",
                $"  at Tests/Equivalence.cs:{LineOf("OneItem()")}"),
            Fail("OneItem").Message);
    }

    [VerifyTest]
    public void Lists_of_different_lengths_give_the_counts_and_compare_the_shared_items()
    {
        Assert.Equal(
            "  Counts: expected 3 items, found 2 items\n  Names: expected 3 items, found 2 items\n  Names[1]: expected \"Cy\", found \"Bo\"",
            string.Join("\n", Fail("Lengths").Message.Split('\n')[1..4]));
        Assert.Equal("  Counts: expected [1], found null", Fail("NullList").Message.Split('\n')[1]);
    }

    [VerifyTest]
    public void A_list_of_records_walks_each_item()
    {
        Assert.Equal(
            Lines(
                "VerifyEquivalent failed: Route(\"Rue\") is not equivalent to Route(\"Quai\")",
                "  Stops[1].Street: expected \"Quai\", found \"Rue\"",
                $"  at Tests/Equivalence.cs:{LineOf("RecordList()")}"),
            Fail("RecordList").Message);
        Assert.Equal(
            "  expected 2 items, found 1 item\n  [0].City: expected \"Paris\", found \"London\"",
            string.Join("\n", Fail("ListRoot").Message.Split('\n')[1..3]));
    }

    [VerifyTest]
    public void Nullable_struct_items_and_nested_arrays_are_walked()
    {
        Assert.Equal(
            "  Marks[0].X: expected 2, found 1\n  Marks[1]: expected Sample.Point, found null",
            string.Join("\n", Fail("NullableItems").Message.Split('\n')[1..3]));
        Assert.Equal("  Grid[1][0]: expected 4, found 2", Fail("NestedArrays").Message.Split('\n')[1]);
    }

    // Until ipjohnson/VerificationGlue#107, a dictionary or a set compares with Equals, which for
    // Dictionary<TKey, TValue> and HashSet<T> compares references.
    [VerifyTest]
    public void A_dictionary_or_set_member_compares_with_Equals()
    {
        Assert.Equal(
            "  Levels: expected [\"gift\": 3], found [\"gift\": 3]\n  Tags: expected [\"fragile\"], found [\"fragile\"]",
            string.Join("\n", Fail("Dictionary").Message.Split('\n')[1..^1]));
    }

    [VerifyTest]
    public void Null_on_either_side_is_a_difference_at_that_path()
    {
        Assert.Equal(
            Lines(
                "VerifyEquivalent failed: new Invoice(7, null, Status.Open) is not equivalent to Invoice(\"Paris\")",
                "  Customer: expected Customer { Name = Ada, Address = Address { City = Paris, Street = High St } }, found null",
                $"  at Tests/Equivalence.cs:{LineOf("NullMember()")}"),
            Fail("NullMember").Message);
        Assert.Equal(
            "VerifyEquivalent failed: null is not equivalent to Invoice(\"Paris\")\n" +
            "  expected Invoice { Id = 7, Customer = Customer { Name = Ada, Address = Address { City = Paris, Street = High St } }, Status = Open }, found null",
            FirstLines(Fail("NullActual").Message, 2));
        Assert.Equal(
            "VerifyEquivalent failed: Invoice(\"Paris\") is not equivalent to null\n" +
            "  expected null, found Invoice { Id = 7, Customer = Customer { Name = Ada, Address = Address { City = Paris, Street = High St } }, Status = Open }",
            FirstLines(Fail("NullExpected").Message, 2));
    }

    [VerifyTest]
    public void An_IEquatable_member_compares_with_Equals()
    {
        Assert.Equal(
            Lines(
                "VerifyEquivalent failed: new Line { Sku = \"A\", Price = new(40, \"EUR\") } is not equivalent to new Line { Sku = \"B\", Price = new(40, \"GBP\") }",
                "  Sku: expected \"B\", found \"A\"",
                "  Price: expected 40 GBP, found 40 EUR",
                $"  at Tests/Equivalence.cs:{LineOf("EquatableMember()")}"),
            Fail("EquatableMember").Message);
    }

    [VerifyTest]
    public void The_message_lists_ten_differences_and_counts_the_rest()
    {
        var lines = Fail("Limit").Message.Split('\n');

        Assert.Equal(13, lines.Length);
        Assert.Equal("  A: expected 1, found 0", lines[1]);
        Assert.Equal("  J: expected 1, found 0", lines[10]);
        Assert.Equal("  and 2 more differences", lines[11]);
        Assert.Equal("  and 1 more difference", Fail("OneMore").Message.Split('\n')[11]);
    }

    [VerifyTest]
    public void Struct_members_are_walked()
    {
        Assert.Equal(
            "  Origin.X: expected 3, found 1\n  Anchor.Y: expected 4, found 2",
            string.Join("\n", Fail("Structs").Message.Split('\n')[1..3]));
        Assert.Equal("  Anchor: expected Sample.Point, found null", Fail("NullableStruct").Message.Split('\n')[1]);
    }

    [VerifyTest]
    public void A_type_with_no_comparer_compares_with_Equals_and_keeps_the_file_and_line()
    {
        Assert.Equal(
            Lines("VerifyEquivalent failed: 3 is not equivalent to 4", "  expected 4, found 3", $"  at Tests/Equivalence.cs:{LineOf("Primitive()")}"),
            Fail("Primitive").Message);
        Assert.Equal(
            Lines(
                "VerifyEquivalent failed: new Hidden(1) is not equivalent to new Hidden(2)",
                "  expected Hidden { Value = 2 }, found Hidden { Value = 1 }",
                $"  at Tests/Equivalence.cs:{LineOf("Private()")}"),
            Fail("Private").Message);
    }

    [VerifyTest]
    public void A_call_in_a_tier_3_copy_calls_the_generated_comparer()
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Assembly.Value.GetType("Sample.Cases")!.GetMethod("InCopy__Verified")!.Invoke(null, null));

        Assert.Equal(
            Lines(
                "VerifyEquivalent failed: Invoice(\"London\") is not equivalent to Invoice(\"Paris\")",
                "  Customer.Address.City: expected \"Paris\", found \"London\"",
                $"  at Tests/Equivalence.cs:{LineOf("InCopy()")}"),
            thrown.InnerException!.Message);
    }

    [VerifyTest]
    public void An_unordered_list_counts_duplicates_and_lists_missing_and_extra_items()
    {
        Run("UnorderedPass");
        Run("UnorderedRecordsPass");
        Assert.Equal(
            Lines(
                "VerifyEquivalent failed: new Batch { Counts = [1, 1, 2] } is not equivalent to new Batch { Counts = [1, 2, 2] }",
                "  Counts: missing 2",
                "  Counts: extra 1",
                $"  at Tests/Equivalence.cs:{LineOf("UnorderedDuplicates()")}"),
            Fail("UnorderedDuplicates").Message);
        Assert.Equal(
            "  Stops: missing Address { City = Paris, Street = Quai }\n  Stops: extra Address { City = London, Street = High St }",
            string.Join("\n", Fail("UnorderedRecords").Message.Split('\n')[1..3]));
        Assert.Equal(
            "  missing Address { City = Paris, Street = Quai }\n  extra Address { City = London, Street = High St }",
            string.Join("\n", Fail("UnorderedRoot").Message.Split('\n')[1..3]));
    }

    [VerifyTest]
    public void Ignore_skips_a_nested_member_and_only_that_member()
    {
        Assert.Equal(
            Lines(
                "VerifyEquivalent failed: Invoice(\"London\") is not equivalent to Invoice(\"Paris\", Status.Paid)",
                "  Status: expected Paid, found Open",
                $"  at Tests/Equivalence.cs:{LineOf("IgnoreNested()")}"),
            Fail("IgnoreNested").Message);
        Run("IgnoreConditional");
        Run("IgnoreAll");

        // The same Invoice and Address types still compare City in a call without the option.
        Assert.Contains("Customer.Address.City: expected \"Paris\", found \"London\"", Fail("NestedRecords").Message);
    }

    [VerifyTest]
    public void An_option_that_VG0010_reports_is_left_out_and_the_others_apply()
    {
        Assert.Equal("  Customer.Address.City: expected \"Paris\", found \"London\"", string.Join("\n", Fail("UnappliedOption").Message.Split('\n')[1..^1]));
        Assert.Equal(
            Lines("VerifyEquivalent failed: 3 is not equivalent to 4", "  expected 4, found 3", $"  at Tests/Equivalence.cs:{LineOf("PrimitiveWithOptions()")}"),
            Fail("PrimitiveWithOptions").Message);
    }

    [VerifyTest]
    public void A_call_with_options_in_a_tier_3_copy_applies_them()
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Assembly.Value.GetType("Sample.Cases")!.GetMethod("InCopyWithOptions__Verified")!.Invoke(null, null));

        Assert.Equal(
            Lines(
                "VerifyEquivalent failed: Invoice(\"London\") is not equivalent to Invoice(\"Paris\", Status.Paid)",
                "  Status: expected Paid, found Open",
                $"  at Tests/Equivalence.cs:{LineOf("InCopyWithOptions()")}"),
            thrown.InnerException!.Message);
    }

    [VerifyTest]
    public void VG0010_reports_an_option_lambda_that_is_not_a_member_access()
    {
        var diagnostics = Analyze(
            "VerifyEquivalent(batch, batch, o => o.Unordered(x => x.Counts.Where(c => c > 0)).Ignore(x => x.Names[0]).Unordered(x => x.Names));",
            "VerifyEquivalent(invoice, invoice, o => o.Ignore(x => x.Customer!.Name.Trim()).Ignore(x => x.Customer!.Name.Length).Ignore(x => x));",
            "VerifyEquivalent(invoice, invoice, o => o.Unordered(x => x.Customer!.Name));",
            "VerifyEquivalent(invoice, invoice, options);",
            "VerifyEquivalent(batch, batch, o => o.Ignore(x => x.Hidden));");

        Assert.Equal(
            [
                "VG0010: The VerifyEquivalent option 'x => x.Counts.Where(c => c > 0)' is not applied, because it is not a chain of member accesses on the lambda's parameter, such as 'x => x.Lines'",
                "VG0010: The VerifyEquivalent option 'x => x.Names[0]' is not applied, because it is not a chain of member accesses on the lambda's parameter, such as 'x => x.Lines'",
                "VG0010: The VerifyEquivalent option 'x => x.Customer!.Name.Trim()' is not applied, because it is not a chain of member accesses on the lambda's parameter, such as 'x => x.Lines'",
                "VG0010: The VerifyEquivalent option 'x => x.Customer!.Name.Length' is not applied, because the comparer compares 'Customer.Name' with Equals, so it does not read 'Length'",
                "VG0010: The VerifyEquivalent option 'x => x' is not applied, because Ignore needs a member, and the lambda names the value itself",
                "VG0010: The VerifyEquivalent option 'x => x.Customer!.Name' is not applied, because 'Customer.Name' is not an array or a list that the comparer walks",
                "VG0010: The VerifyEquivalent option 'options' is not applied, because it is not a lambda that calls Unordered and Ignore on its parameter",
                "VG0010: The VerifyEquivalent option 'x => x.Hidden' is not applied, because 'Hidden' is not a public field or property that the comparer reads",
            ],
            diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}"));
        Assert.All(diagnostics, diagnostic => Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity));
        Assert.Equal("x => x.Counts.Where(c => c > 0)", diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan));
    }

    [VerifyTest]
    public void The_interceptor_calls_a_comparer_written_for_the_type()
    {
        var result = GeneratorHarness.Generate(GeneratorHarness.Compilation((Path, Source)), new VerificationGlueGenerator(), out _);

        var interceptors = result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith(".Interceptors.g.cs", StringComparison.Ordinal)).ToString();
        var comparers = result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith(EquivalenceEmitter.HintName, StringComparison.Ordinal)).ToString();
        Assert.Contains("VerifyEquivalent_", interceptors);
        Assert.Contains("(global::Sample.Invoice actual, global::Sample.Invoice expected, string? actualExpression = null, string? expectedExpression = null)", interceptors);
        Assert.Contains("global::VerificationGlue.Generated.Equivalence.Verify_Invoice_", interceptors);
        Assert.Contains("differences.Equal(path, \"City\", actual.City, expected.City);", comparers);
        Assert.Contains("Compare_AddressList_", comparers);
        Assert.Contains("(global::System.Collections.Generic.IReadOnlyList<global::Sample.Address> actual, ", comparers);
        Assert.Contains("differences.ItemCount(path, actual.Length, expected.Length);", comparers);
        Assert.Contains("differences.Equal(path, \"Levels\", actual.Levels, expected.Levels);", comparers);
        Assert.Contains(
            "(global::Sample.Invoice actual, global::Sample.Invoice expected, global::System.Func<global::VerificationGlue.EquivalenceOptions<global::Sample.Invoice>, global::VerificationGlue.EquivalenceOptions<global::Sample.Invoice>> options, string? actualExpression = null",
            interceptors);
        Assert.Contains("<T>(T actual, T expected, global::System.Func<global::VerificationGlue.EquivalenceOptions<T>, global::VerificationGlue.EquivalenceOptions<T>> options, ", interceptors);
        Assert.Contains("Verify_InvoiceOptions_", interceptors);
        Assert.Contains("differences.Same(actual[other], expected[index], Compare_Address_", comparers);
        Assert.Contains("differences.Same(actual[other], expected[index]))", comparers);
        Assert.DoesNotContain("Reflection", comparers);
        Assert.DoesNotContain("GetType", comparers);
    }

    private static ImmutableArray<Diagnostic> Analyze(params string[] statements) =>
        GeneratorHarness.Analyze($$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using VerificationGlue;
            using static VerificationGlue.VerifySyntax;

            public sealed record Customer(string Name);
            public sealed record Invoice(int Id, Customer? Customer);
            public sealed class Batch
            {
                public int[] Counts { get; set; } = [];
                public IReadOnlyList<string> Names { get; set; } = [];
                internal int Hidden { get; set; }
            }

            static class Tests
            {
                static void M(Batch batch, Invoice invoice, Func<EquivalenceOptions<Invoice>, EquivalenceOptions<Invoice>> options)
                {
                    {{string.Join("\n            ", statements)}}
                }
            }
            """);

    private static void Run(string method) => Assembly.Value.GetType("Sample.Cases")!.GetMethod(method)!.Invoke(null, null);

    private static VerifyException Fail(string method)
    {
        var thrown = Assert.Throws<TargetInvocationException>(() => Run(method));
        return Assert.IsType<VerifyException>(thrown.InnerException);
    }

    private static string Lines(params string[] lines) => string.Join("\n", lines);

    private static string FirstLines(string text, int count) => string.Join("\n", text.Split('\n').Take(count));

    private static int LineOf(string method) =>
        Array.FindIndex(Source.Split('\n'), line => line.Contains($" {method}", StringComparison.Ordinal)) + 1;
}

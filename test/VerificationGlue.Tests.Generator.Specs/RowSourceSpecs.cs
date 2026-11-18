namespace VerificationGlue.Tests.Generator.Specs;

/// <summary>
/// <c>[RowSource]</c> and <c>ITestDataSource</c> rows are read at discovery. A row of primitives, strings or
/// enums takes its ID from its values, a keyed row from its key, and any other row from its position only
/// when the source declares deterministic replay. A source that cannot give each row one stable ID fails
/// under the method's ID and runs no row.
/// </summary>
public class RowSourceSpecs
{
    private const string Totals = "Shop.OrderTests.Totals(System.Int32,System.Int64,System.String)";
    private const string Bills = "Shop.OrderTests.Bills(Shop.Customer)";

    private static string Source(string members, string tests) => $$"""
        using VerificationGlue.Tests;
        using static VerificationGlue.VerifySyntax;

        namespace Shop;

        public sealed record Customer(string Name, int Total);

        public sealed class CustomersAttribute(string mode) : Attribute, ITestDataSource
        {
            public bool Replay { get; set; }

            public bool DeterministicReplay => Replay;

            public async IAsyncEnumerable<TestRow> GetRowsAsync(TestDataContext context)
            {
                await Task.Yield();
                var customers = new[] { new Customer("ann", 5), new Customer("bob", 7) };
                if (mode == "reversed") customers = [customers[1], customers[0]];
                if (mode == "throws") throw new InvalidOperationException("The customer list is offline.");
                if (mode == "empty") yield break;
                if (mode == "wrong type") { yield return new TestRow("ann"); yield break; }
                foreach (var customer in customers)
                {
                    yield return mode == "unkeyed" ? new TestRow(customer) : new TestRow(customer) { Key = mode == "same key" ? "one" : customer.Name };
                }
            }
        }

        public partial class OrderTests
        {
            {{members}}

            {{tests}}
        }
        """;

    private static string Tuples(string rows) => Source(
        $"public static IEnumerable<(int, int, string?)> Orders() => [{rows}];",
        "[VerifyTest] [RowSource(nameof(Orders))] public void Totals(int qty, long price, string? note) => Verify(qty * price > 0);");

    private static string Customers(string arguments) => Source("", $"[VerifyTest] [Customers({arguments})] public void Bills(Customer customer) => Verify(customer.Total > 0);");

    [VerifyTest]
    public async Task Each_row_of_a_tuple_source_is_a_test_with_an_id_from_its_values()
    {
        var tests = Registrations.Load(Tuples("(2, 10, \"gift\"), (3, 5, null)"), rewrite: false, new TestGenerator());

        Assert.Equal(
            [
                Totals + "#row(System.Int32:2,System.Int64:10,System.String:\"gift\")",
                Totals + "#row(System.Int32:3,System.Int64:5,null)",
            ],
            tests.Keys);
        foreach (var test in tests.Values)
        {
            Assert.Empty(await test());
        }
    }

    [VerifyTest]
    public void A_reordered_source_of_primitive_values_keeps_each_row_id_and_name()
    {
        var before = Registrations.Names(Tuples("(2, 10, \"gift\"), (3, 5, null)"));
        var after = Registrations.Names(Tuples("(3, 5, null), (1, 1, \"new\"), (2, 10, \"gift\")"));

        Assert.Equal(3, after.Length);
        Assert.All(before, row => Assert.Contains(row, after));
        Assert.Contains((Totals + "#row(System.Int32:3,System.Int64:5,null)", "Totals(qty: 3, price: 5, note: null)"), after);
    }

    [VerifyTest]
    public void Identical_rows_of_primitive_values_get_an_occurrence_suffix()
    {
        var names = Registrations.Names(Tuples("(2, 10, null), (2, 10, null)"));

        var row = Totals + "#row(System.Int32:2,System.Int64:10,null)";
        Assert.Equal([row, row + "#2"], names.Select(name => name.Uid));
    }

    [VerifyTest]
    public async Task A_keyed_data_source_attribute_gives_ids_from_its_keys_that_survive_a_reorder()
    {
        var tests = Registrations.Load(Customers("\"\""), rewrite: false, new TestGenerator());
        var reversed = Registrations.Names(Customers("\"reversed\""));

        Assert.Equal([Bills + "#key(\"ann\")", Bills + "#key(\"bob\")"], tests.Keys);
        Assert.Equal(tests.Keys.Order(), reversed.Select(name => name.Uid).Order());
        Assert.Contains((Bills + "#key(\"ann\")", "Bills(customer: Customer { Name = ann, Total = 5 })"), reversed);
        foreach (var test in tests.Values)
        {
            Assert.Empty(await test());
        }
    }

    [VerifyTest]
    public void A_source_that_declares_deterministic_replay_gives_unkeyed_rows_ids_from_their_positions()
    {
        var names = Registrations.Names(Customers("\"unkeyed\", Replay = true"));

        Assert.Equal([Bills + "#index(0)", Bills + "#index(1)"], names.Select(name => name.Uid));
    }

    [VerifyTest]
    [Row("\"unkeyed\"", "Row 0 of the data source CustomersAttribute of " + Bills + " has a value that is not a primitive, a string or an enum, and the row has no TestRow.Key.")]
    [Row("\"throws\"", "The data source CustomersAttribute of " + Bills + " threw while it listed its rows: The customer list is offline.")]
    [Row("\"empty\"", "The data source CustomersAttribute of " + Bills + " gave no rows.")]
    [Row("\"same key\"", "The data source CustomersAttribute of " + Bills + " gives the key \"one\" to more than one row.")]
    [Row("\"wrong type\"", "Row 0 of the data source CustomersAttribute of " + Bills + " gives \"ann\" (System.String) for the parameter 'customer' of type Shop.Customer.")]
    public async Task A_source_that_cannot_give_each_row_a_stable_id_fails_under_the_method_id_and_runs_no_row(string arguments, string message)
    {
        var tests = Registrations.Load(Customers(arguments), rewrite: false, new TestGenerator());

        var test = Assert.Single(tests);
        Assert.Equal(Bills, test.Key);
        var failure = Assert.Single(await test.Value());
        Assert.StartsWith(message, failure.Message);
    }

    [VerifyTest]
    public async Task Methods_properties_fields_and_async_members_can_supply_rows()
    {
        var tests = Registrations.Load(
            Source(
                """
                public static IEnumerable<int> Quantities => [1, 2];
                internal static readonly List<(int, string)> Pairs = [(3, "c")];
                public static async IAsyncEnumerable<(int Qty, string Label)> Later()
                {
                    await Task.Yield();
                    yield return (4, "d");
                }
                """,
                """
                [VerifyTest] [RowSource(nameof(Quantities))] [RowSource(nameof(Pairs))] [RowSource(nameof(Later))] [Row(5, "e")]
                public void Labels(int qty, string label = "none") => Verify(qty > 0 && label.Length > 0);
                """),
            rewrite: false,
            new TestGenerator());

        const string Labels = "Shop.OrderTests.Labels(System.Int32,System.String)";
        Assert.Equal(
            [
                Labels + "#row(System.Int32:5,System.String:\"e\")",
                Labels + "#row(System.Int32:1)",
                Labels + "#row(System.Int32:2)",
                Labels + "#row(System.Int32:3,System.String:\"c\")",
                Labels + "#row(System.Int32:4,System.String:\"d\")",
            ],
            tests.Keys);
        foreach (var test in tests.Values)
        {
            Assert.Empty(await test());
        }
    }
}

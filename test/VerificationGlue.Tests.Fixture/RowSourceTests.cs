using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace Fixture;

public sealed record Customer(string Name, int Total);

public sealed record Payment(string Method, int Amount);

/// <summary>
/// Rows for <see cref="RowSourceTests"/> that the specs change between processes through the environment.
/// <c>VG_FIXTURE_SOURCE_MODE</c> is <c>source=mode</c>, where mode is <c>reorder</c>, <c>throw</c>, <c>empty</c>,
/// <c>duplicate</c> or <c>unkeyed</c>. It applies from the process that <c>VG_FIXTURE_SOURCE_FROM</c> names,
/// counted from 1 in the log, so a retry's fresh process can see different data from the first one. Reads
/// are logged, and processes counted, only when <c>VG_FIXTURE_SOURCE_FROM</c> is set.
/// With <c>VG_FIXTURE_SOURCE_FAIL_FIRST</c> set, the first row of each test fails on its first attempt.
/// </summary>
public static class Sources
{
    private static readonly string? s_mode = Environment.GetEnvironmentVariable("VG_FIXTURE_SOURCE_MODE");
    private static readonly string? s_fromSetting = Environment.GetEnvironmentVariable("VG_FIXTURE_SOURCE_FROM");
    private static readonly int s_from = int.TryParse(s_fromSetting, out var from) ? from : 1;

    // The lifecycle specs compare the whole log, so reads are logged only for the specs that count processes.
    private static readonly bool s_logReads = s_fromSetting is not null;
    private static readonly bool s_failFirst = Environment.GetEnvironmentVariable("VG_FIXTURE_SOURCE_FAIL_FIRST") is not null;

    /// <summary><paramref name="rows"/> as this process sees them, and the mode that applies to them.</summary>
    public static IReadOnlyList<T> Read<T>(string source, IReadOnlyList<T> rows, out string? mode)
    {
        var process = Log.Lines.Count(line => line.StartsWith($"source {source} read", StringComparison.Ordinal)) + 1;
        mode = process >= s_from && s_mode is { } setting && setting.StartsWith(source + "=", StringComparison.Ordinal) ? setting[(source.Length + 1)..] : null;
        IReadOnlyList<T> read = mode switch
        {
            "reorder" => rows.Reverse().ToArray(),
            "throw" => throw new InvalidOperationException($"The {source} source is offline."),
            "empty" => [],
            "duplicate" => [.. rows, rows[0]],
            _ => rows,
        };
        if (s_logReads)
        {
            Log.Write($"source {source} read in process {process}: {string.Join(", ", read)}");
        }

        return read;
    }

    /// <summary>Logs that <paramref name="row"/> ran, and fails its first attempt when it is the first row and the specs ask for it.</summary>
    public static void Ran(string source, string row, bool first)
    {
        var line = $"{source} ran {row} in attempt ";
        var attempt = Log.Lines.Count(logged => logged.StartsWith(line, StringComparison.Ordinal)) + 1;
        Log.Write(line + attempt);
        Verify(!(s_failFirst && first && attempt == 1));
    }
}

/// <summary>A keyed source. In <c>unkeyed</c> mode its rows have no keys, and in <c>duplicate</c> mode two rows share a key.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CustomersAttribute : Attribute, ITestDataSource
{
    public async IAsyncEnumerable<TestRow> GetRowsAsync(TestDataContext context)
    {
        await Task.Yield();
        var customers = Sources.Read("customers", [new Customer("ann", 5), new Customer("bob", 7), new Customer("cy", 9)], out var mode);
        foreach (var customer in customers)
        {
            yield return mode == "unkeyed" ? new TestRow(customer) : new TestRow(customer) { Key = customer.Name };
        }
    }
}

public partial class RowSourceTests
{
    public static IEnumerable<(int Qty, string Item)> Orders() => Sources.Read("orders", [(1, "pen"), (2, "ink"), (3, "pad")], out _);

    public static IEnumerable<Payment> Payments => Sources.Read("payments", [new Payment("card", 10), new Payment("cash", 20)], out _);

    [VerifyTest]
    [RowSource(nameof(Orders))]
    public void Ships(int qty, string item) => Sources.Ran("orders", $"{qty} {item}", qty == 1);

    [VerifyTest]
    [Customers]
    public void Bills(Customer customer) => Sources.Ran("customers", customer.Name, customer.Name == "ann");

    [VerifyTest]
    [RowSource(nameof(Payments), DeterministicReplay = true)]
    public void Pays(Payment payment) => Sources.Ran("payments", payment.Method, payment.Method == "card");

    [VerifyTest]
    [Row("gift")]
    [Row("box")]
    public void Packs(string label) => Sources.Ran("literal", label, label == "gift");
}

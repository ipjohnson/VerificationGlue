using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VerificationGlue.Tests;

/// <summary>
/// Takes the rows of a <c>[VerifyTest]</c> method from a static member of the test class. The member is a
/// method without parameters, a property or a field that returns <c>IEnumerable&lt;T&gt;</c> or
/// <c>IAsyncEnumerable&lt;T&gt;</c>, where <c>T</c> is a tuple of the values of the method's first
/// parameters, or the value of the first parameter. The rows are read at discovery, and each row is its
/// own test. A missing member, or one with the wrong shape, fails the build with VGT0005.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class RowSourceAttribute : Attribute
{
    /// <summary>Takes the rows from <paramref name="memberName"/>.</summary>
    /// <param name="memberName">The member, written as <c>nameof(Member)</c>.</param>
    public RowSourceAttribute(string memberName) => MemberName = memberName;

    /// <summary>The static member that returns the rows.</summary>
    public string MemberName { get; }

    /// <summary>
    /// Declares that the member returns the same rows in the same order in every process. A row whose
    /// values are not all primitives, strings or enums then takes its test ID from its position.
    /// Without it, such a row fails discovery.
    /// </summary>
    public bool DeterministicReplay { get; set; }
}

/// <summary>
/// Supplies the rows of the <c>[VerifyTest]</c> methods it is placed on. An attribute that implements it
/// is read at discovery, and each row it returns is its own test.
/// </summary>
public interface ITestDataSource
{
    /// <summary>The rows of the test that <paramref name="context"/> describes.</summary>
    /// <param name="context">The test method.</param>
    IAsyncEnumerable<TestRow> GetRowsAsync(TestDataContext context);

    /// <summary>
    /// True when the source returns the same rows in the same order in every process: at discovery,
    /// when the tests run, and in a retry. A row without a <see cref="TestRow.Key"/> whose values are not
    /// all primitives, strings or enums then takes its test ID from its position. Without it, such a row
    /// fails discovery.
    /// </summary>
    bool DeterministicReplay => false;
}

/// <summary>
/// One row from an <see cref="ITestDataSource"/>. A row whose values are all primitives, strings or enums
/// takes its test ID from its values. Set <see cref="Key"/> for any other row.
/// </summary>
public sealed class TestRow
{
    /// <summary>Creates a row.</summary>
    /// <param name="values">The values of the method's first parameters, in order.</param>
    public TestRow(params object?[]? values) => Values = values ?? [null];

    /// <summary>The values of the method's first parameters, in order.</summary>
    public IReadOnlyList<object?> Values { get; }

    /// <summary>
    /// A key that names this row and no other row of the test, and stays the same when the source adds,
    /// removes or reorders rows. When it is set, the test ID comes from it rather than from the values.
    /// </summary>
    public string? Key { get; init; }
}

/// <summary>The test method whose rows an <see cref="ITestDataSource"/> returns.</summary>
public sealed class TestDataContext
{
    private readonly TestDescriptor _test;

    internal TestDataContext(TestDescriptor test, CancellationToken cancellationToken)
    {
        _test = test;
        CancellationToken = cancellationToken;
    }

    /// <summary>The ID of the test method, which each row's test ID starts with.</summary>
    public string Uid => _test.Uid;

    /// <summary>The type that declares the test method.</summary>
    public Type TestClass => _test.TestClass;

    /// <summary>The test method's name.</summary>
    public string MethodName => _test.MethodName;

    /// <summary>The parameters of the test method.</summary>
    public IReadOnlyList<TestParameter> Parameters => _test.Parameters;

    /// <summary>Cancelled when the test session is cancelled.</summary>
    public CancellationToken CancellationToken { get; }
}

/// <summary>A data source that lists no rows, throws, or gives rows that cannot each have one stable test ID.</summary>
internal sealed class TestDataSourceException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Expands the tests that have data sources into one test per row, at discovery and in each process
/// that runs tests, so a test ID selects the same row in a fresh process.
/// </summary>
internal static class RowExpansion
{
    /// <summary>
    /// <paramref name="tests"/> with each test that has data sources replaced by its rows, or by one test
    /// under the method's ID that fails with the reason when a source fails.
    /// </summary>
    public static async Task<TestDescriptor[]> ExpandAsync(IReadOnlyList<TestDescriptor> tests, CancellationToken cancellationToken)
    {
        var expanded = new List<TestDescriptor>(tests.Count);
        foreach (var test in tests)
        {
            if (test.CreateDataSources is null)
            {
                expanded.Add(test);
                continue;
            }

            try
            {
                expanded.AddRange(await RowsAsync(test, cancellationToken));
            }
            catch (Exception failure) when (!cancellationToken.IsCancellationRequested)
            {
                var reason = failure as TestDataSourceException
                    ?? new TestDataSourceException("The data sources of " + test.Uid + " could not be created: " + failure.Message, failure);
                expanded.Add(test.WithSourceFailure(reason));
            }
        }

        return expanded.ToArray();
    }

    private static async Task<List<TestDescriptor>> RowsAsync(TestDescriptor test, CancellationToken cancellationToken)
    {
        var sources = test.CreateDataSources!(test);
        if (sources.Count == 0)
        {
            throw new TestDataSourceException($"{test.Uid} has no data source that the runner can create. Look for warning VGT0006 on its attributes.");
        }

        var context = new TestDataContext(test, cancellationToken);
        var rows = new List<TestDescriptor>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var position = 0; position < sources.Count; position++)
        {
            var source = sources[position];
            var name = Name(source);
            var index = 0;
            try
            {
                await foreach (var row in source.GetRowsAsync(context).WithCancellation(cancellationToken))
                {
                    if (row is null)
                    {
                        throw new TestDataSourceException($"The data source {name} of {test.Uid} gave null for row {index}.");
                    }

                    Check(test, name, index, row);
                    string identity;
                    if (row.Key is { } key)
                    {
                        identity = "#key(" + Quote(key) + ")";
                        if (!keys.Add(identity))
                        {
                            throw new TestDataSourceException(
                                $"The data source {name} of {test.Uid} gives the key {Quote(key)} to more than one row. Each TestRow.Key must be unique, so that a test ID always selects the same row.");
                        }
                    }
                    else if (ValueIdentity(row.Values) is { } values)
                    {
                        var occurrence = occurrences.TryGetValue(values, out var count) ? count + 1 : 1;
                        occurrences[values] = occurrence;
                        identity = values + (occurrence > 1 ? "#" + occurrence.ToString(CultureInfo.InvariantCulture) : "");
                    }
                    else if (source.DeterministicReplay)
                    {
                        identity = "#index(" + (sources.Count > 1 ? position.ToString(CultureInfo.InvariantCulture) + "," : "") + index.ToString(CultureInfo.InvariantCulture) + ")";
                    }
                    else
                    {
                        throw new TestDataSourceException(
                            $"Row {index} of the data source {name} of {test.Uid} has a value that is not a primitive, a string or an enum, and the row has no TestRow.Key. " +
                            "Give each row a key, or declare that the source returns the same rows in the same order in every process (DeterministicReplay), so that a test ID always selects the same row.");
                    }

                    rows.Add(test.WithRow(test.Uid + identity, row.Values.ToArray()));
                    index++;
                }
            }
            catch (Exception failure) when (failure is not TestDataSourceException && !cancellationToken.IsCancellationRequested)
            {
                throw new TestDataSourceException($"The data source {name} of {test.Uid} threw while it listed its rows: {failure.Message}", failure);
            }

            if (index == 0)
            {
                throw new TestDataSourceException($"The data source {name} of {test.Uid} gave no rows. A source must give at least one row, so that the test cannot pass without running.");
            }
        }

        return rows;
    }

    /// <summary>Fails when the row has more values than the method has parameters, or a value that its parameter cannot take.</summary>
    private static void Check(TestDescriptor test, string name, int index, TestRow row)
    {
        var parameters = test.Parameters;
        if (row.Values.Count == 0 || row.Values.Count > parameters.Count)
        {
            throw new TestDataSourceException(
                $"Row {index} of the data source {name} of {test.Uid} has {row.Values.Count} values, but the method has {parameters.Count} parameter{(parameters.Count == 1 ? "" : "s")}.");
        }

        for (var i = 0; i < row.Values.Count; i++)
        {
            var value = row.Values[i];
            var type = parameters[i].Type;
            var accepts = value is null
                ? !type.IsValueType || Nullable.GetUnderlyingType(type) is not null
                : (Nullable.GetUnderlyingType(type) ?? type).IsInstanceOfType(value);
            if (!accepts)
            {
                throw new TestDataSourceException(
                    $"Row {index} of the data source {name} of {test.Uid} gives {DefaultNameFormatter.FormatValue(value)} ({value?.GetType().FullName ?? "null"}) for the parameter '{parameters[i].Name}' of type {parameters[i].TypeName}.");
            }
        }
    }

    private static string Name(ITestDataSource source) => source is IRowSourceName named ? named.Name : source.GetType().Name;

    /// <summary>
    /// <c>#row(Type:value,...)</c>, in the form a literal <c>[Row]</c> ID takes, when every value is null, a
    /// primitive, a decimal, a string or an enum. Null otherwise.
    /// </summary>
    private static string? ValueIdentity(IReadOnlyList<object?> values)
    {
        var text = new StringBuilder("#row(");
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                text.Append(',');
            }

            var value = values[i];
            if (value is null)
            {
                text.Append("null");
                continue;
            }

            var type = value.GetType();
            string? canonical = value switch
            {
                string item => Quote(item),
                char item => Quote(item.ToString()).Replace('"', '\''),
                bool item => item ? "true" : "false",
                float item => item.ToString("R", CultureInfo.InvariantCulture),
                double item => item.ToString("R", CultureInfo.InvariantCulture),
                Enum item => Convert.ToString(Convert.ChangeType(item, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                decimal or sbyte or byte or short or ushort or int or uint or long or ulong or nint or nuint => Convert.ToString(value, CultureInfo.InvariantCulture),
                _ => null,
            };
            if (canonical is null)
            {
                return null;
            }

            text.Append((type.FullName ?? type.Name).Replace('+', '.')).Append(':').Append(canonical);
        }

        return text.Append(')').ToString();
    }

    private static string Quote(string value)
    {
        var text = new StringBuilder("\"");
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    text.Append("\\\"");
                    break;
                case '\\':
                    text.Append("\\\\");
                    break;
                case '\n':
                    text.Append("\\n");
                    break;
                case '\r':
                    text.Append("\\r");
                    break;
                case '\t':
                    text.Append("\\t");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(character);
                    }

                    break;
            }
        }

        return text.Append('"').ToString();
    }
}

/// <summary>A data source with a name for messages, such as <c>[RowSource("Prices")]</c>.</summary>
internal interface IRowSourceName
{
    string Name { get; }
}

/// <summary>The rows of a <c>[RowSource]</c> member, each converted to the method's parameter types by generated code.</summary>
internal sealed class MemberRowSource<T>(string name, Func<IAsyncEnumerable<T>> rows, Func<T, object?[]> values, bool deterministicReplay)
    : ITestDataSource, IRowSourceName
{
    public string Name { get; } = name;

    public bool DeterministicReplay { get; } = deterministicReplay;

    public async IAsyncEnumerable<TestRow> GetRowsAsync(TestDataContext context)
    {
        await foreach (var row in rows().WithCancellation(context.CancellationToken))
        {
            yield return new TestRow(values(row));
        }
    }
}

/// <summary>The data sources that generated registrations create.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class TestDataSources
{
    /// <summary>The rows of a <c>[RowSource]</c> member that returns <c>IEnumerable&lt;T&gt;</c>.</summary>
    /// <param name="memberName">The member's name.</param>
    /// <param name="rows">Reads the member.</param>
    /// <param name="values">Converts a row to the values of the method's first parameters.</param>
    /// <param name="deterministicReplay">The <c>DeterministicReplay</c> of the attribute.</param>
    public static ITestDataSource Member<T>(string memberName, Func<IEnumerable<T>> rows, Func<T, object?[]> values, bool deterministicReplay) =>
        new MemberRowSource<T>(Name(memberName), () => ToAsync(rows()), values, deterministicReplay);

    /// <summary>The rows of a <c>[RowSource]</c> member that returns <c>IAsyncEnumerable&lt;T&gt;</c>.</summary>
    /// <param name="memberName">The member's name.</param>
    /// <param name="rows">Reads the member.</param>
    /// <param name="values">Converts a row to the values of the method's first parameters.</param>
    /// <param name="deterministicReplay">The <c>DeterministicReplay</c> of the attribute.</param>
    public static ITestDataSource AsyncMember<T>(string memberName, Func<IAsyncEnumerable<T>> rows, Func<T, object?[]> values, bool deterministicReplay) =>
        new MemberRowSource<T>(Name(memberName), rows, values, deterministicReplay);

    /// <summary>
    /// <paramref name="members"/>, then the attributes of <paramref name="test"/>'s method that implement
    /// <see cref="ITestDataSource"/>, in source order.
    /// </summary>
    /// <param name="test">The test method.</param>
    /// <param name="members">The <c>[RowSource]</c> members.</param>
    public static IReadOnlyList<ITestDataSource> With(TestDescriptor test, params ITestDataSource[] members) =>
        [.. members, .. test.CreateMethodAttributes().OfType<ITestDataSource>()];

    private static string Name(string memberName) => "[RowSource(\"" + memberName + "\")]";

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> rows, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }

        await Task.CompletedTask;
    }
}

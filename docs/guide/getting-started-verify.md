# Getting started with Verify

At the end of this page you can write checks with `Verify`, `Throws`, `ThrowsExactly` and `ThrowsAsync` in xunit, NUnit, MSTest or TUnit. You can also read the message a failed check gives, including under Native AOT.

This page covers messages built from the source text and the shape of each check. [Soft checks with Check](check.md) covers `Check`, and [How tiers work](tiers.md) covers messages with captured values.

## Install

Add the `VerificationGlue` package to the test project:

<!-- snippet: install-verificationglue -->
<a id='snippet-install-verificationglue'></a>
```csproj
<ItemGroup>
  <PackageReference Include="VerificationGlue" Version="$(VerificationGlueVersion)" />
</ItemGroup>
```
<sup><a href='/samples/VerifyAot/VerifyAot.csproj#L16-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-install-verificationglue' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The sample sets `VerificationGlueVersion` to the version it tests. A project writes the version number there.

The package adds a source generator and needs no other setup. Its `buildTransitive/VerificationGlue.props` adds `VerificationGlue.Generated` to `InterceptorsNamespaces`, which the generated code needs.

## Write checks

Import the methods with `using static VerificationGlue.VerifySyntax;`. The checks are the same in every test framework. Only the test attribute changes.

| Call | What it does |
|---|---|
| `Verify(() => condition)` | Throws a `VerifyException` when `condition` is false. The message lists the value of each part of `condition`. |
| `Verify(condition)` | Throws a `VerifyException` when `condition` is false. The message gives the source text and the shape of `condition`. |
| `Verify(condition, "...")` | Adds the message to the failure. The failure still shows the source text of `condition`. |
| `Verify(condition, $"...")` | Adds the interpolated message to the failure. Its holes run only when `condition` is false. |
| `Throws<T>(action)` | Returns the exception that `action` throws. Fails when it throws nothing, or throws an exception that is not a `T` or derived from `T`. |
| `ThrowsAsync<T>(action)` | Awaits `action` and does the same as `Throws<T>`. |
| `ThrowsExactly<T>(action)` | Returns the exception that `action` throws. Fails unless its type is exactly `T`, so an exception derived from `T` fails. |
| `ThrowsExactlyAsync<T>(action)` | Awaits `action` and does the same as `ThrowsExactly<T>`. |
| `Single(items)` | Returns the only item. Fails when there are no items or more than one. |
| `Single(items, predicate)` | Returns the only item that matches `predicate`. Fails when none or more than one match. |
| `Is<T>(value)` | Returns `value` as a `T`. Fails when `value` is null or not a `T`. |
| `NotNull(value)` | Returns `value` when it is not null. Nullable analysis treats the result as non-null. |
| `VerifyEquivalent(actual, expected)` | Compares the two values member by member, and arrays and lists item by item. Fails with the path of each member that differs. [Compare two values with VerifyEquivalent](verify-equivalent.md) has the details. |

`Throws`, `Single`, `Is` and `NotNull` return a value, so they throw on failure also inside a `VerifyScope`. `VerifyEquivalent` throws there too, like `Verify`. The [messages page](messages.md#single) shows what each one reports.

Write a condition as a lambda by default. Keep `Verify(condition)` in three cases:

- the condition uses `await`, which a lambda passed as `Func<bool>` cannot contain;
- the condition reads a `Span<T>` or another `ref struct`, which a lambda cannot capture;
- the next lines rely on a null check in the condition. Only `Verify(condition)` tells the compiler that the value is not null afterwards.

<!-- snippet: keep-verify-bool-in-xunit -->
<a id='snippet-keep-verify-bool-in-xunit'></a>
```cs
[Fact]
public async Task Some_checks_keep_Verify_bool()
{
    var orders = new Order?[] { new Order(Id: 7, Total: 40) };

    // A Func<bool> cannot contain await.
    Verify(await Task.FromResult(orders.Length) == 1);

    // A lambda cannot capture a Span<T>.
    ReadOnlySpan<Order?> span = orders;
    Verify(span.Length == 1);

    // Only Verify(bool) tells the compiler that first is not null on the next line.
    var first = orders[0];
    Verify(first is not null);
    Verify(() => first.Total == 40);
}
```
<sup><a href='/samples/VerifyInXunit/OrderTests.cs#L62-L80' title='Snippet source file'>snippet source</a> | <a href='#snippet-keep-verify-bool-in-xunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Analyzer [VG0009](../diagnostics/VG0009.md) suggests the lambda form for every other `Verify(condition)` and `Check(condition)` in a test method.

### xunit

<!-- snippet: verify-in-xunit -->
<a id='snippet-verify-in-xunit'></a>
```cs
[Fact]
public async Task Checkout_reports_an_empty_cart()
{
    var order = new Order(Id: 7, Total: 40);
    Verify(() => order.Total == 40);
    Verify(order.Id > 0, $"order {order.Id} has no id");

    var error = Throws<InvalidOperationException>(() => new Cart().Checkout());
    Verify(() => error.Message == "The cart is empty.");

    var exact = ThrowsExactly<InvalidOperationException>(() => new Cart().Checkout());
    Verify(() => exact.Message == "The cart is empty.");

    var timeout = await ThrowsAsync<TimeoutException>(() => new Client().GetAsync("/orders/7"));
    Verify(() => timeout.Message.Contains("/orders/7"));
}
```
<sup><a href='/samples/VerifyInXunit/OrderTests.cs#L10-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-in-xunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### NUnit

<!-- snippet: verify-in-nunit -->
<a id='snippet-verify-in-nunit'></a>
```cs
[Test]
public async Task Checkout_reports_an_empty_cart()
{
    var order = new Order(Id: 7, Total: 40);
    Verify(() => order.Total == 40);
    Verify(order.Id > 0, $"order {order.Id} has no id");

    var error = Throws<InvalidOperationException>(() => new Cart().Checkout());
    Verify(() => error.Message == "The cart is empty.");

    var exact = ThrowsExactly<InvalidOperationException>(() => new Cart().Checkout());
    Verify(() => exact.Message == "The cart is empty.");

    var timeout = await ThrowsAsync<TimeoutException>(() => new Client().GetAsync("/orders/7"));
    Verify(() => timeout.Message.Contains("/orders/7"));
}
```
<sup><a href='/samples/VerifyInNUnit/OrderTests.cs#L10-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-in-nunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### MSTest

<!-- snippet: verify-in-mstest -->
<a id='snippet-verify-in-mstest'></a>
```cs
[TestMethod]
public async Task Checkout_reports_an_empty_cart()
{
    var order = new Order(Id: 7, Total: 40);
    Verify(() => order.Total == 40);
    Verify(order.Id > 0, $"order {order.Id} has no id");

    var error = Throws<InvalidOperationException>(() => new Cart().Checkout());
    Verify(() => error.Message == "The cart is empty.");

    var exact = ThrowsExactly<InvalidOperationException>(() => new Cart().Checkout());
    Verify(() => exact.Message == "The cart is empty.");

    var timeout = await ThrowsAsync<TimeoutException>(() => new Client().GetAsync("/orders/7"));
    Verify(() => timeout.Message.Contains("/orders/7"));
}
```
<sup><a href='/samples/VerifyInMSTest/OrderTests.cs#L11-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-in-mstest' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### TUnit

<!-- snippet: verify-in-tunit -->
<a id='snippet-verify-in-tunit'></a>
```cs
[Test]
public async Task Checkout_reports_an_empty_cart()
{
    var order = new Order(Id: 7, Total: 40);
    Verify(() => order.Total == 40);
    Verify(order.Id > 0, $"order {order.Id} has no id");

    var error = Throws<InvalidOperationException>(() => new Cart().Checkout());
    Verify(() => error.Message == "The cart is empty.");

    var exact = ThrowsExactly<InvalidOperationException>(() => new Cart().Checkout());
    Verify(() => exact.Message == "The cart is empty.");

    var timeout = await ThrowsAsync<TimeoutException>(() => new Client().GetAsync("/orders/7"));
    Verify(() => timeout.Message.Contains("/orders/7"));
}
```
<sup><a href='/samples/VerifyInTUnit/OrderTests.cs#L10-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-in-tunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each sample runs in CI with the framework's own runner.

## Read a failure

This test makes a lambda check fail on purpose and checks its message:

<!-- snippet: lambda-in-xunit -->
<a id='snippet-lambda-in-xunit'></a>
```cs
[Fact]
public void A_lambda_lists_the_value_of_each_part()
{
    var order = new Order(Id: 7, Total: 40);

    var failure = Throws<VerifyException>(() => Verify(() => order.Total == 30));

    Verify(() => failure.Message == """
        Verify failed: order.Total == 30
          order.Total → 40
          order       → Order { Id = 7, Total = 40 }
          at OrderTests.cs:51
        """);
}
```
<sup><a href='/samples/VerifyInXunit/OrderTests.cs#L45-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-lambda-in-xunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The first line names the call and shows the body of the lambda. Each next line gives the value of a part of the condition. The last line gives the file, relative to the project directory, and the line of the call.

`Verify(condition)` without a lambda gives the shape of the condition instead of the values:

<!-- snippet: failure-in-xunit -->
<a id='snippet-failure-in-xunit'></a>
```cs
[Fact]
public void A_failure_names_the_shape_file_and_line()
{
    var order = new Order(Id: 7, Total: 40);

    var failure = Throws<VerifyException>(() => Verify(order.Total == 30));

    Verify(() => failure.Message == """
        Verify failed: order.Total == 30
          expected order.Total to equal 30
          at OrderTests.cs:35
        """);
}
```
<sup><a href='/samples/VerifyInXunit/OrderTests.cs#L29-L43' title='Snippet source file'>snippet source</a> | <a href='#snippet-failure-in-xunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The first line names the call and shows the source text of its argument. The next line describes the failure from the shape of the condition. The last line gives the file, relative to the project directory, and the line of the call. A `Verify` call with a message adds the message before the file and line.

The generator picks the second line from the shape of the condition:

| Condition | Second line |
|---|---|
| `x != null`, `x is not null`, `x is { }` | `x can not be null` |
| `x == null`, `x is null` | `expected x to be null` |
| `a == b` | `expected a to equal b` |
| `a != b` | `expected a to not equal b` |
| `a < b`, `a <= b`, `a > b`, `a >= b` | `expected a to be less than b`, `to be at most b`, `to be greater than b`, `to be at least b` |
| `x is T`, `x is T name` | `expected x to be of type T` |
| `x is { Total: > 50 }` and other patterns | `expected x to match { Total: > 50 }` |
| `!x` | `expected x to be false` |
| A call that returns `bool` | `expected call to return true` |
| `Throws<T>(() => call)`, `ThrowsAsync<T>(() => call)` | `expected call to throw T` |
| `ThrowsExactly<T>(() => call)`, `ThrowsExactlyAsync<T>(() => call)` | `expected call to throw exactly T`, or `expected exactly T, but U was thrown` when the call throws a type `U` derived from `T` |
| Any other condition, or an operand written on several lines | No second line. The first line already shows the source text. |

A `Throws`, `ThrowsAsync`, `ThrowsExactly` or `ThrowsExactlyAsync` failure adds a line that says what happened: `no exception was thrown`, or the type and message of the exception that was thrown. That exception is also the `InnerException` of the `VerifyException`.

[`InterceptorSpecs.cs`](../../test/VerificationGlue.Generator.Specs/InterceptorSpecs.cs) runs every row of the table. [Generated interceptors](generated/interceptors.md) shows the code the generator writes for the xunit sample.

## Native AOT

The file, the line and the second line are fixed at compile time, so a Native AOT build reports the same message. This holds even though stack traces under Native AOT carry no file or line.

<!-- snippet: verify-under-native-aot -->
<a id='snippet-verify-under-native-aot'></a>
```cs
var total = 40;
var failure = Throws<VerifyException>(() => Verify(total == 30, $"total is {total}"));
Console.WriteLine(failure.Message);
```
<sup><a href='/samples/VerifyAot/Program.cs#L4-L8' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-under-native-aot' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`eng/pack-and-consume.sh` publishes this sample with Native AOT and warnings as errors, runs it, and checks each message it prints.

## Without the generator

When the generator does not run, `Verify` still fails with the source text of the condition. The message has no second line and no file or line:

<!-- snippet: tier-1-message -->
<a id='snippet-tier-1-message'></a>
```cs
var count = 2;

var failure = Assert.Throws<VerifyException>(() => Verify(count == 3));

Assert.Equal("Verify failed: count == 3", failure.Message);
```
<sup><a href='/test/VerificationGlue.Specs/VerifySyntaxSpecs.cs#L20-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-tier-1-message' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`VerifyEquivalent` without the generator compares the two values with `Equals`, so a record compares member by member and a class compares by reference. Its message gives both values and says that the generator did not run. The [messages page](messages.md#verifyequivalent) shows the message with the generator.

## Captured values

`Verify(() => condition)` lists the value of each part of its condition in any method. With tier 3 on, `Verify(condition)` in a `[Verify]` method does the same. [How tiers work](tiers.md) explains how to turn it on, and the [messages page](messages.md) shows each message.

## Build warnings

The package reports conditions that cannot check what they seem to check:

- [VG0002](../diagnostics/VG0002.md): the condition is a constant.
- [VG0003](../diagnostics/VG0003.md): the condition changes state.
- [VG0004](../diagnostics/VG0004.md): the condition compares `float` or `double` values with `==` or `!=`.

It also reports what keeps a method from tier 3, and a misplaced `Check`:

- [VG0001](../diagnostics/VG0001.md): `.Because` or `.WithMessage` where tier 3 does not rewrite the call.
- [VG0005](../diagnostics/VG0005.md): a `[Verify]` method in a type that is not `partial`.
- [VG0006](../diagnostics/VG0006.md): a part of a condition that cannot be captured.
- [VG0007](../diagnostics/VG0007.md): `Check` where no `VerifyScope` can be open.
- [VG0008](../diagnostics/VG0008.md): a `[Verify]` method in a file that declares a `file` type.

It suggests the lambda form in a test method:

- [VG0009](../diagnostics/VG0009.md): `Verify(condition)` or `Check(condition)` where `Verify(() => condition)` loses nothing. Set `VerificationGlueLambdaSeverity` to `warning` to make it a build warning.

It reports a `VerifyEquivalent` option that the comparer cannot apply, and a type that it cannot walk:

- [VG0010](../diagnostics/VG0010.md): an `Unordered` or `Ignore` lambda that is not a chain of member accesses, or that names a member the comparer does not read.
- [VG0011](../diagnostics/VG0011.md): a type that the comparer compares by reference or with `ValueType.Equals`, because it has no public field or property or the generated code cannot name it, and an open generic type.

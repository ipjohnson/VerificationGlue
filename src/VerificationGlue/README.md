![VerificationGlue](https://raw.githubusercontent.com/ipjohnson/VerificationGlue/main/assets/logo/lockup.png)

# VerificationGlue

Checks for .NET tests whose failure messages name the file, the line and the shape of the condition, and with tier 3 on, the value of each part. They work under xunit, NUnit, MSTest, TUnit and plain application code, and give the same message under Native AOT.

## Install

<!-- snippet: install-verificationglue -->
<a id='snippet-install-verificationglue'></a>
```csproj
<ItemGroup>
  <PackageReference Include="VerificationGlue" Version="$(VerificationGlueVersion)" />
</ItemGroup>
```
<sup><a href='/samples/VerifyAot/VerifyAot.csproj#L16-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-install-verificationglue' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Use

Import the checks with `using static VerificationGlue.VerifySyntax;`:

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

A failed `Verify(() => order.Total == 30)` lists the value of each part, in any test framework:

```text
Verify failed: order.Total == 30
  order.Total → 40
  order       → Order { Id = 7, Total = 40 }
  at OrderTests.cs:51
```

A source generator in the package writes the code that records the values, and the file and line, at compile time. Nothing is looked up at run time, so the message is the same under Native AOT.

Keep `Verify(order.Total == 30)` without a lambda in three cases: the condition uses `await`, it reads a `Span<T>`, or the next lines rely on a null check in it. That form reports the shape of the condition, such as `expected order.Total to equal 30`. Analyzer VG0009 suggests the lambda form everywhere else in a test.

`Check` records a failure and lets the test continue. A `VerifyScope` reports every recorded failure when it ends.

## Learn more

- [Getting started with Verify](../../docs/guide/getting-started-verify.md), with a section per test framework and every message shape.
- [How tiers work and how to get values](../../docs/guide/tiers.md) and [Soft checks with Check](../../docs/guide/check.md).
- [Failure messages](../../docs/guide/messages.md), the real output of each kind of failure.
- [Migrate from xunit Assert](../../docs/guide/migrate-from-xunit-assert.md).
- [Generated interceptors](../../docs/guide/generated/interceptors.md) and [generated tier 3 copies](../../docs/guide/generated/rewritten-methods.md), the code the generator writes.
- Diagnostics [VG0001](../../docs/diagnostics/VG0001.md) to [VG0009](../../docs/diagnostics/VG0009.md).

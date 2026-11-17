# VerificationGlue

Checks for .NET tests whose failure messages name the file, the line and the shape of the condition. They work under xunit, NUnit, MSTest, TUnit and plain application code, and give the same message under Native AOT. This version is a preview.

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
    Verify(order.Total == 40);
    Verify(order.Id > 0, $"order {order.Id} has no id");

    var error = Throws<InvalidOperationException>(() => new Cart().Checkout());
    Verify(error.Message == "The cart is empty.");

    var timeout = await ThrowsAsync<TimeoutException>(() => new Client().GetAsync("/orders/7"));
    Verify(timeout.Message.Contains("/orders/7"));
}
```
<sup><a href='/samples/VerifyInXunit/OrderTests.cs#L10-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-verify-in-xunit' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A failed `Verify(order.Total == 30)` reports:

```text
Verify failed: order.Total == 30
  expected order.Total to equal 30
  at OrderTests.cs:32
```

A source generator in the package writes the second and third lines at compile time. Nothing is looked up at run time, so the message is the same under Native AOT.

## Learn more

- [Getting started with Verify](../../docs/guide/getting-started-verify.md), with a section per test framework and every message shape.
- [Generated interceptors](../../docs/guide/generated/interceptors.md), the code the generator writes.
- Build warnings [VG0002](../../docs/diagnostics/VG0002.md), [VG0003](../../docs/diagnostics/VG0003.md) and [VG0004](../../docs/diagnostics/VG0004.md).

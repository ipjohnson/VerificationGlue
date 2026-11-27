# Give tests a service collection

At the end of this page you can give tests a Microsoft.Extensions.DependencyInjection container with `[ServiceCollection<T>]` and `[ServiceCollection]`, replace a service with a `[Mock]` parameter, add DependencyModules modules to the container, and share one container across a class.

## Reference the package

The test project references `VerificationGlue.Tests` and `VerificationGlue.Tests.DependencyInjection`. Both have the same version. The package brings Microsoft.Extensions.DependencyInjection and a generator.

## Name the services with `[ServiceCollection<T>]`

`T` is a class with a public static `Configure` method that registers services:

<!-- snippet: service-collection-shop-services -->
<a id='snippet-service-collection-shop-services'></a>
```cs
public sealed class ShopServices
{
    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton<IOrderStore, InMemoryOrderStore>();
        services.AddTransient<OrderService>();
        services.AddScoped<Basket>();
    }
}
```
<sup><a href='/samples/TestsDependencyInjection/Services.cs#L20-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-shop-services' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Put `[ServiceCollection<ShopServices>]` on a test class. Each test then gets a container with those services, and its parameters come from it. A `[Mock]` parameter replaces the registration of its type, so `OrderService` receives the mock:

<!-- snippet: service-collection-mock -->
<a id='snippet-service-collection-mock'></a>
```cs
[ServiceCollection<ShopServices>]
public partial class OrderServiceTests
{
    [VerifyTest]
    public void Create_stamps_the_order_with_the_clock([Mock] IClock clock, OrderService sut, IOrderStore store)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        var order = sut.Create(quantity: 2, price: 10);

        Verify(order.Created == new DateTime(2026, 11, 17));
        Verify(store.Orders.Count == 1);
    }
}
```
<sup><a href='/samples/TestsDependencyInjection/ServiceCollectionTests.cs#L10-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-mock' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The generator writes the call to `Configure`, so the container calls it with no reflection. A `T` with no such method fails the build with [VGD0001](../diagnostics/VGD0001.md). `[ServiceCollection<string>]` is one example.

`Configure` can take a second parameter of `TestContainerContext`. It holds the scope, the attributes of the scope and the test's output:

<!-- snippet: service-collection-payment-services -->
<a id='snippet-service-collection-payment-services'></a>
```cs
public sealed class PaymentServices
{
    public static void Configure(IServiceCollection services, TestContainerContext context)
    {
        context.Output.WriteLine($"Payment services for {context.Uid}");
        services.AddKeyedSingleton<IPaymentGateway, AcceptAll>("card");
        services.AddKeyedSingleton<IPaymentGateway, AcceptAll>("invoice");
        services.AddTransient<CheckoutService>();
    }
}
```
<sup><a href='/samples/TestsDependencyInjection/Services.cs#L32-L43' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-payment-services' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Replace a keyed service

A `[Mock]` parameter with `[FromKeyedServices(key)]` replaces the registration under that key. The other keys keep their registrations:

<!-- snippet: service-collection-keyed-mock -->
<a id='snippet-service-collection-keyed-mock'></a>
```cs
[ServiceCollection<PaymentServices>]
public partial class CheckoutTests
{
    [VerifyTest]
    public void The_card_declines([Mock][FromKeyedServices("card")] IPaymentGateway card, CheckoutService sut)
    {
        card.Charge(10).Returns(false);

        Verify(!sut.Pay(10, byInvoice: false));
        Verify(sut.Pay(10, byInvoice: true));
    }
}
```
<sup><a href='/samples/TestsDependencyInjection/ServiceCollectionTests.cs#L27-L40' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-keyed-mock' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Sources add up

`[ServiceCollection<T>]` goes on the assembly, a class or a method. The container of a test calls the `Configure` method of each one that applies, from the assembly to the class to the method. A source on the class does not replace the one on the assembly. A registration from the method comes last, so it wins when it registers the same service.

This assembly registers the clock for every test:

<!-- snippet: service-collection-assembly -->
<a id='snippet-service-collection-assembly'></a>
```cs
[assembly: ServiceCollection<ClockServices>]
```
<sup><a href='/samples/TestsDependencyInjection/Services.cs#L6-L8' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-assembly' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: service-collection-clock-services -->
<a id='snippet-service-collection-clock-services'></a>
```cs
public sealed class ClockServices
{
    public static void Configure(IServiceCollection services) =>
        services.AddSingleton<IClock, SystemClock>();
}
```
<sup><a href='/samples/TestsDependencyInjection/Services.cs#L12-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-clock-services' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The class adds the shop's services, and one method adds a fixed clock:

<!-- snippet: service-collection-add-up -->
<a id='snippet-service-collection-add-up'></a>
```cs
[ServiceCollection<ShopServices>]
public partial class SourceOrderTests
{
    [VerifyTest]
    public void The_assembly_and_the_class_add_up(OrderService sut, IClock clock)
    {
        Verify(clock is SystemClock);
        Verify(sut.Create(quantity: 1, price: 3).Total == 3);
    }

    [VerifyTest]
    [ServiceCollection<FixedClockServices>]
    public void The_method_registers_last(OrderService sut)
    {
        Verify(sut.Create(quantity: 1, price: 3).Created == new DateTime(2030, 1, 1));
    }
}
```
<sup><a href='/samples/TestsDependencyInjection/ServiceCollectionTests.cs#L42-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-add-up' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The test's `[Mock]` parameters are registered after every source. The modules of [Add DependencyModules modules](#add-dependencymodules-modules) are applied before every `Configure` method.

## Use `[ServiceCollection]` without a type

`[ServiceCollection]` gives the tests a container from the sources of the wider targets, and adds no source of its own. Here the class gets the clock from the assembly:

<!-- snippet: service-collection-plain -->
<a id='snippet-service-collection-plain'></a>
```cs
[ServiceCollection]
public partial class ClockTests
{
    [VerifyTest]
    public void Gets_the_assembly_clock(IClock clock)
    {
        Verify(clock is SystemClock);
    }
}
```
<sup><a href='/samples/TestsDependencyInjection/ServiceCollectionTests.cs#L62-L72' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-plain' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Add DependencyModules modules

When the test project references DependencyModules, a module attribute adds that module's services to the container. The module can come from a referenced project or from the test project. No VerificationGlue package references DependencyModules, so the test project references it:

<!-- snippet: reference-dependency-modules -->
<a id='snippet-reference-dependency-modules'></a>
```csproj
<ItemGroup>
  <ProjectReference Include="../ModulesShop/ModulesShop.csproj" />
  <PackageReference Include="DependencyModules.Runtime" />
  <PackageReference Include="DependencyModules.SourceGenerator" PrivateAssets="all" />
</ItemGroup>
```
<sup><a href='/samples/TestsDependencyModules/TestsDependencyModules.csproj#L21-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-reference-dependency-modules' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`SutModule` is the module of a referenced project. `DependencyModules.SourceGenerator` writes its `[SutModule]` attribute:

<!-- snippet: sut-module -->
<a id='snippet-sut-module'></a>
```cs
[DependencyModule]
public partial class SutModule;
```
<sup><a href='/samples/ModulesShop/Shop.cs#L7-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-sut-module' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Give the assembly `[ServiceCollection]`, and put `[SutModule]` on a test class. Each test then gets the module's services, and a `[Mock]` parameter replaces the matching registration, so `OrderService` receives the mock:

<!-- snippet: modules-assembly -->
<a id='snippet-modules-assembly'></a>
```cs
[assembly: ServiceCollection]
[assembly: AuditModule]
```
<sup><a href='/samples/TestsDependencyModules/Modules.cs#L8-L11' title='Snippet source file'>snippet source</a> | <a href='#snippet-modules-assembly' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: module-on-class -->
<a id='snippet-module-on-class'></a>
```cs
[SutModule]
public partial class OrderTests
{
    [VerifyTest]
    public void Stamps_the_order(OrderService sut, [Mock] IClock clock)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17));

        Verify(sut.Create(quantity: 2, price: 10).Created == clock.Now);
    }
}
```
<sup><a href='/samples/TestsDependencyModules/ModuleTests.cs#L9-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-module-on-class' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A module declared in the test project works the same way:

<!-- snippet: receipt-module -->
<a id='snippet-receipt-module'></a>
```cs
[DependencyModule(OnlyRealm = true)]
public partial class ReceiptModule : IServiceCollectionConfiguration
{
    public void ConfigureServices(IServiceCollection services) =>
        services.AddTransient<ReceiptPrinter>();
}
```
<sup><a href='/samples/TestsDependencyModules/Modules.cs#L55-L62' title='Snippet source file'>snippet source</a> | <a href='#snippet-receipt-module' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: module-in-test-project -->
<a id='snippet-module-in-test-project'></a>
```cs
[ReceiptModule]
public partial class ReceiptTests
{
    [VerifyTest]
    public void Prints_the_time([Mock] IClock clock, ReceiptPrinter printer)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17, 9, 30, 0));

        Verify(printer.Print(12) == "12 at 09:30");
    }
}
```
<sup><a href='/samples/TestsDependencyModules/ModuleTests.cs#L23-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-module-in-test-project' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Where a module attribute goes

A module attribute goes on the assembly, a class or a method. The modules are applied from the assembly to the class to the method, so the method's registration wins when two modules register the same service. `[assembly: AuditModule]` above reaches every test, and a class module comes after it:

<!-- snippet: module-on-assembly -->
<a id='snippet-module-on-assembly'></a>
```cs
public partial class AuditTests
{
    [VerifyTest]
    public void Every_test_gets_the_assembly_module(IAuditLog log)
    {
        Verify(log is AuditLog);
    }
}

[QuietAuditModule]
public partial class QuietAuditTests
{
    [VerifyTest]
    public void The_class_module_is_applied_after_the_assembly_module(IAuditLog log)
    {
        Verify(log is QuietAuditLog);
    }
}
```
<sup><a href='/samples/TestsDependencyModules/ModuleTests.cs#L37-L56' title='Snippet source file'>snippet source</a> | <a href='#snippet-module-on-assembly' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A module on a method reaches only that test. The `Configure` method of a `[ServiceCollection<T>]` is called after every module, so it can replace a module's registration too:

<!-- snippet: module-on-method -->
<a id='snippet-module-on-method'></a>
```cs
[SutModule]
public partial class ClockTests
{
    [VerifyTest]
    public void Gets_the_clock_of_the_class_module(IClock clock)
    {
        Verify(clock is SystemClock);
    }

    [VerifyTest]
    [FixedClockModule]
    public void The_method_module_wins(OrderService sut)
    {
        Verify(sut.Create(quantity: 1, price: 3).Created == new DateTime(2030, 1, 1));
    }

    [VerifyTest]
    [ServiceCollection<FixedClockServices>]
    public void Configure_is_called_after_the_modules(IClock clock)
    {
        Verify(clock is FixedClock);
    }
}
```
<sup><a href='/samples/TestsDependencyModules/ModuleTests.cs#L58-L82' title='Snippet source file'>snippet source</a> | <a href='#snippet-module-on-method' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Modules with arguments

A module with constructor parameters and settable properties gets an attribute with the same parameters and properties. The module's attribute passes them to the module:

<!-- snippet: mail-module -->
<a id='snippet-mail-module'></a>
```cs
[DependencyModule(OnlyRealm = true)]
public partial class MailModule(string host, int port) : IServiceCollectionConfiguration
{
    public string? Sender { get; set; }

    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton(new MailSettings(host, port, Sender));

    public override bool Equals(object? obj) =>
        obj is MailModule other && other.Settings == Settings;

    public override int GetHashCode() => Settings.GetHashCode();

    private MailSettings Settings => new(host, port, Sender);
}
```
<sup><a href='/samples/ModulesShop/Shop.cs#L73-L89' title='Snippet source file'>snippet source</a> | <a href='#snippet-mail-module' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: greeting-module -->
<a id='snippet-greeting-module'></a>
```cs
[DependencyModule(OnlyRealm = true)]
public partial class GreetingModule(string word, int times) : IServiceCollectionConfiguration
{
    public string Punctuation { get; set; } = ".";

    private Greeting Greeting => new(string.Join(" ", Enumerable.Repeat(word + Punctuation, times)));

    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton(Greeting);

    public override bool Equals(object? obj) =>
        obj is GreetingModule other && other.Greeting == Greeting;

    public override int GetHashCode() => Greeting.GetHashCode();
}
```
<sup><a href='/samples/TestsDependencyModules/Modules.cs#L84-L100' title='Snippet source file'>snippet source</a> | <a href='#snippet-greeting-module' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: module-arguments -->
<a id='snippet-module-arguments'></a>
```cs
[MailModule("smtp.example.com", 25, Sender = "shop@example.com")]
public partial class ModuleArgumentTests
{
    [VerifyTest]
    public void A_referenced_module_gets_its_arguments(MailSettings settings)
    {
        Verify(settings == new MailSettings("smtp.example.com", 25, "shop@example.com"));
    }

    [VerifyTest]
    [GreetingModule("Hello", 2, Punctuation = "!")]
    public void A_test_project_module_gets_its_arguments(Greeting greeting)
    {
        Verify(greeting.Text == "Hello! Hello!");
    }
}
```
<sup><a href='/samples/TestsDependencyModules/ModuleTests.cs#L84-L101' title='Snippet source file'>snippet source</a> | <a href='#snippet-module-arguments' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Name a module with `[ServiceCollection<T>]`

`T` can be a module that has a public parameterless constructor. The container adds `new T()` with the other modules of the test:

<!-- snippet: service-collection-module -->
<a id='snippet-service-collection-module'></a>
```cs
[ServiceCollection<SutModule>]
public partial class NamedModuleTests
{
    [VerifyTest]
    public void Gets_the_services_of_the_module(OrderService sut, IOrderStore store)
    {
        sut.Create(quantity: 1, price: 2);

        Verify(store.Orders.Count == 1);
    }

    [VerifyTest]
    [ServiceCollection<ReceiptModule>]
    public void Names_a_module_of_the_test_project([Mock] IClock clock, ReceiptPrinter printer)
    {
        clock.Now.Returns(new DateTime(2026, 11, 17, 18, 5, 0));

        Verify(printer.Print(3) == "3 at 18:05");
    }
}
```
<sup><a href='/samples/TestsDependencyModules/ModuleTests.cs#L103-L124' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-module' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`T` counts as a module when it implements `IDependencyModule` or is a class marked `[DependencyModule]`. A `T` that has a `Configure` method is called through `Configure`. A module without a public parameterless constructor fails the build with [VGD0001](../diagnostics/VGD0001.md).

A test project that does not reference DependencyModules builds without this code, and its generated code names no DependencyModules type. `eng/pack-and-consume.sh` publishes `samples/ModulesConsumer`, a test project with its own modules, with Native AOT and runs its tests.

## Share a container across a class or the assembly

`Scope = ContainerScope.Class` builds one container for the tests of the class, from the attributes of the class and the assembly. `Scope = ContainerScope.Assembly` builds one from the attributes of the assembly. Each test gets its own service scope, so a scoped service such as `Basket` is new in each test, and a singleton such as the store is shared:

<!-- snippet: service-collection-shared -->
<a id='snippet-service-collection-shared'></a>
```cs
[ServiceCollection<ShopServices>(Scope = ContainerScope.Class)]
public partial class OrderHistoryTests
{
    private static readonly ConcurrentBag<(Basket Basket, IOrderStore Store)> s_seen = [];

    [VerifyTest]
    public void Saves_an_order(OrderService sut, IOrderStore store, Basket basket)
    {
        s_seen.Add((basket, store));
        var order = sut.Create(quantity: 3, price: 2);

        Verify(store.Orders.Contains(order));
        Verify(s_seen.All(seen => seen.Store == store));
        Verify(s_seen.Count(seen => seen.Basket == basket) == 1);
    }

    [VerifyTest]
    public void Sees_the_same_store(IOrderStore store, Basket basket)
    {
        s_seen.Add((basket, store));

        Verify(s_seen.All(seen => seen.Store == store));
        Verify(s_seen.Count(seen => seen.Basket == basket) == 1);
    }
}
```
<sup><a href='/samples/TestsDependencyInjection/ServiceCollectionTests.cs#L74-L100' title='Snippet source file'>snippet source</a> | <a href='#snippet-service-collection-shared' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A shared container does not read the attributes of a narrower target. `[ServiceCollection<T>(Scope = ContainerScope.Class)]` on a method, or `Scope = ContainerScope.Assembly` on a class or a method, fails the build with [VGD0002](../diagnostics/VGD0002.md).

A test of a shared container cannot take a `[Mock]` parameter. The service scope adds no registrations, so the services would not receive the mock. The build fails with [VGT0008](../diagnostics/VGT0008.md). Use the default scope for tests with mocks.

## Next

- [Containers and scopes](containers-and-scopes.md)
- [Parameters and where they come from](test-parameters.md)

using ModulesShop;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using VerificationGlue.Tests.DependencyInjection;
using static VerificationGlue.VerifySyntax;

namespace TestsDependencyModules;

#region module-on-class
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
#endregion

#region module-in-test-project
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
#endregion

#region module-on-assembly
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
#endregion

#region module-on-method
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
#endregion

#region module-arguments
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
#endregion

#region service-collection-module
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
#endregion

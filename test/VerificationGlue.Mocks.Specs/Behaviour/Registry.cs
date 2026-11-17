// case: mocks-behaviour-0005
// layer: behaviour
// origin: mocks.md, Discovery and Registry
// expect: pass

namespace VerificationGlue.Mocks.Specs.Behaviour;

public class Registry
{
    [Mock]
    public IGreeter? Greeter { get; set; }

    [Fact]
    public void A_generic_helper_creates_a_mock_named_in_GenerateMock()
    {
        var clock = Make<IClock>();

        clock.Now.Returns(new DateTime(2026, 11, 17));

        Assert.Equal(new DateTime(2026, 11, 17), clock.Now);
    }

    [Fact]
    public void MockRegistry_creates_a_mock_from_a_type_named_only_in_a_Mock_attribute()
    {
        var greeter = (IGreeter)MockRegistry.Create(typeof(IGreeter));

        greeter.Greet("Ada").Returns("Hello, Ada");

        Assert.Equal("Hello, Ada", greeter.Greet("Ada"));
    }

    [Fact]
    public void A_type_with_no_generated_mock_fails_with_a_message_that_names_the_fix()
    {
        var error = Assert.Throws<InvalidOperationException>(() => MockRegistry.Create(typeof(IDisposable)));

        Assert.Contains("[assembly: GenerateMock(typeof(IDisposable))]", error.Message);
    }

    private static T Make<T>()
        where T : class => Mock.Of<T>();
}

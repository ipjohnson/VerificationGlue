namespace NSubstituteComparison.Cases;

/// <summary>Lets the cases create a mock with <c>Mock.Of&lt;T&gt;()</c> under NSubstitute too.</summary>
public static class Mock
{
    public static T Of<T>()
        where T : class => Substitute.For<T>();
}

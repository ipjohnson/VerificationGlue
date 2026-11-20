using VerificationGlue.Mocks;

namespace Hardening.Specs.ShapeConsumers;

/// <summary>
/// The generic helper of docs/guide/mock-a-type-from-a-generic-helper.md. <c>Mock.Of&lt;T&gt;()</c>
/// here names only a type parameter, so each type it creates is named in
/// <c>[assembly: GenerateMock]</c> in GenerateMocks.cs.
/// </summary>
public static class Fakes
{
    public static T Fresh<T>()
        where T : class => Mock.Of<T>();
}

/// <summary>The public ways to create a mock, each giving a new, unconfigured mock.</summary>
internal static class Paths
{
    /// <summary>
    /// Runs <paramref name="scenario"/> on a mock from each path: <c>Mock.Of&lt;T&gt;()</c> written at
    /// the call site, a <c>[Mock]</c> test parameter, <c>MockRegistry.Create(Type)</c> and the generic helper.
    /// </summary>
    public static void Each<T>(T fromMockOf, T fromParameter, Action<T> scenario)
        where T : class
    {
        foreach (var mock in Mocks(fromMockOf, fromParameter))
        {
            scenario(mock);
        }
    }

    /// <summary>The same, for a type that a test method's signature cannot name, so it has no <c>[Mock]</c> parameter.</summary>
    public static void Each<T>(T fromMockOf, Action<T> scenario)
        where T : class
    {
        foreach (var mock in Mocks(fromMockOf, null))
        {
            scenario(mock);
        }
    }

    public static async Task EachAsync<T>(T fromMockOf, T fromParameter, Func<T, Task> scenario)
        where T : class
    {
        foreach (var mock in Mocks(fromMockOf, fromParameter))
        {
            await scenario(mock);
        }
    }

    private static T[] Mocks<T>(T fromMockOf, T? fromParameter)
        where T : class
    {
        T[] mocks = fromParameter is null
            ? [fromMockOf, (T)MockRegistry.Create(typeof(T)), Fakes.Fresh<T>()]
            : [fromMockOf, fromParameter, (T)MockRegistry.Create(typeof(T)), Fakes.Fresh<T>()];
        Assert.All(mocks, mock => Assert.IsAssignableFrom<IMock>(mock));
        Assert.Equal(mocks.Length, mocks.Distinct(ReferenceEqualityComparer.Instance).Count());
        return mocks;
    }
}

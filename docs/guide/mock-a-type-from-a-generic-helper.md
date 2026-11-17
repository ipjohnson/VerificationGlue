# Mock a type from a generic helper

After reading this page you can create mocks inside a generic helper, such as a test base class or a fixture builder.

The generator writes a mock for each type it sees named in `Mock.Of<T>()`, in a `[Mock]` attribute or in `[assembly: GenerateMock(typeof(T))]`. In a generic helper, `Mock.Of<T>()` names only the type parameter:

<!-- snippet: mocks-generic-helper -->
<a id='snippet-mocks-generic-helper'></a>
```cs
public static class Fakes
{
    public static T Fresh<T>()
        where T : class => Mock.Of<T>();
}
```
<sup><a href='/samples/MocksGuide/GenericHelper.cs#L11-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-generic-helper' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Name each type the helper creates in an assembly attribute:

<!-- snippet: mocks-generate-mock -->
<a id='snippet-mocks-generate-mock'></a>
```cs
[assembly: GenerateMock(typeof(MocksGuide.IClock))]
```
<sup><a href='/samples/MocksGuide/GenericHelper.cs#L5-L7' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-generate-mock' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The helper then returns a working mock:

<!-- snippet: mocks-generic-helper-use -->
<a id='snippet-mocks-generic-helper-use'></a>
```cs
var clock = Fakes.Fresh<IClock>();
clock.Now.Returns(new DateTime(2026, 11, 17));

Verify(clock.Now.Year == 2026);
```
<sup><a href='/samples/MocksGuide/GenericHelper.cs#L24-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-mocks-generic-helper-use' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

When a call to the helper passes a type that nothing names, `Mock.Of<T>()` throws `InvalidOperationException` at run time. [VGM0004](../diagnostics/VGM0004.md) reports that at build time for calls in the same project.

A framework hook that only has a `Type` uses `MockRegistry.Create(type)` instead. [Getting started](getting-started-mocks.md#host-frameworks) shows the hooks.

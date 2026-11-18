using VerificationGlue.Tests;

namespace VerificationGlue.Tests.AspNetCore;

/// <summary>Calls an app's entry point with no reflection.</summary>
public interface IWebAppEntry
{
    /// <summary>The name of the app's assembly. MVC finds the app's controllers by it.</summary>
    static abstract string ApplicationName { get; }

    /// <summary>Calls the app's entry point and returns its exit code. It returns when the app stops.</summary>
    /// <param name="args">The command-line arguments for the app.</param>
    static abstract int Run(string[] args);
}

/// <summary>
/// Starts the app that <typeparamref name="TEntry"/> names for each test, on TestServer, and stops it
/// when the test ends. A test takes <see cref="System.Net.Http.HttpClient"/>,
/// <see cref="Microsoft.Extensions.Hosting.IHost"/> or any service of the app as a parameter, and its
/// <c>[Mock]</c> parameters are registered in the app.
/// With <c>Scope = ContainerScope.Class</c> or <c>ContainerScope.Assembly</c>, one app serves the tests of the
/// class or the assembly. Each test gets its own client and service scope, and a test cannot take a
/// <c>[Mock]</c> parameter (VGT0008).
/// </summary>
/// <typeparam name="TEntry">The app's entry type.</typeparam>
public sealed class WebAppAttribute<TEntry> : ContainerAttribute<WebAppBootstrap<TEntry>>
    where TEntry : IWebAppEntry;

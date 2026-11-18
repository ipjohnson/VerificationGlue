using System;
using System.Collections.Generic;

namespace VerificationGlue.Tests.AspNetCore;

/// <summary>
/// Sets one configuration value of the app that <c>[WebApp&lt;TEntry&gt;]</c> starts. Values add up from the
/// assembly to the class to the method, and a narrower value for the same key replaces a wider one. The
/// value replaces the app's value from <c>appsettings.json</c> and environment variables.
/// </summary>
/// <param name="key">The configuration key, with <c>:</c> between sections, such as <c>Orders:MaxQuantity</c>.</param>
/// <param name="value">The value.</param>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class AppSettingAttribute(string key, string value) : Attribute
{
    /// <summary>The configuration key.</summary>
    public string Key { get; } = key;

    /// <summary>The value.</summary>
    public string Value { get; } = value;
}

/// <summary>
/// Sets the environment name of the app that <c>[WebApp&lt;TEntry&gt;]</c> starts. The narrowest of the
/// assembly, the class and the method wins. Without it the name is <c>Development</c>.
/// </summary>
/// <param name="name">The environment name, such as <c>Staging</c>.</param>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AppEnvironmentAttribute(string name) : Attribute
{
    /// <summary>The environment name.</summary>
    public string Name { get; } = name;
}

/// <summary>
/// Leaves on the watcher that reloads the app's configuration files when they change. Without it the
/// app does not watch them, which saves about 50 ms of each start.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class WatchAppConfigurationAttribute : Attribute;

/// <summary>
/// Runs the app on Kestrel at <c>http://127.0.0.1</c> on a free port instead of on TestServer. The test's
/// <see cref="System.Net.Http.HttpClient"/> has the app's address as its <c>BaseAddress</c>. Goes on the
/// method, the class or the assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class KestrelAttribute : Attribute;

/// <summary>What the configuration attributes of a scope ask of the app.</summary>
internal sealed class AppConfiguration
{
    public const string DefaultEnvironment = "Development";
    public const string LoopbackUrl = "http://127.0.0.1:0";

    private AppConfiguration(string environment, bool watch, bool kestrel, Dictionary<string, string?> settings)
    {
        Environment = environment;
        Watch = watch;
        Kestrel = kestrel;
        Settings = settings;
    }

    public string Environment { get; }

    public bool Watch { get; }

    public bool Kestrel { get; }

    public IReadOnlyDictionary<string, string?> Settings { get; }

    /// <summary>Reads <paramref name="attributes"/>, which run from the widest scope to the narrowest.</summary>
    public static AppConfiguration Read(IReadOnlyList<Attribute> attributes)
    {
        var environment = DefaultEnvironment;
        var watch = false;
        var kestrel = false;
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in attributes)
        {
            switch (attribute)
            {
                case AppSettingAttribute setting:
                    settings[setting.Key] = setting.Value;
                    break;
                case AppEnvironmentAttribute named:
                    environment = named.Name;
                    break;
                case WatchAppConfigurationAttribute:
                    watch = true;
                    break;
                case KestrelAttribute:
                    kestrel = true;
                    break;
            }
        }

        if (kestrel)
        {
            // GenericWebHostService reads urls from the app's configuration when the server starts, so
            // the in-memory source also reaches an app that does not pass args to its builder.
            settings["urls"] = LoopbackUrl;
        }

        return new AppConfiguration(environment, watch, kestrel, settings);
    }

    /// <summary>The command-line arguments for the entry point of <paramref name="applicationName"/>.</summary>
    public string[] Arguments(string applicationName)
    {
        var args = new List<string>
        {
            $"--applicationName={applicationName}",
            $"--contentRoot={AppContext.BaseDirectory}",
            $"--environment={Environment}",
        };
        if (!Watch)
        {
            args.Add("--hostBuilder:reloadConfigOnChange=false");
        }

        foreach (var setting in Settings)
        {
            args.Add($"--{setting.Key}={setting.Value}");
        }

        return [.. args];
    }
}

using System.Reflection;

namespace NSubstituteComparison;

/// <summary>One case in one case assembly.</summary>
public sealed class ComparisonCase
{
    private readonly MethodInfo _method;

    private ComparisonCase(MethodInfo method, AreaAttribute area, CaseAttribute @case)
    {
        _method = method;
        Area = method.DeclaringType!.Name;
        AreaTitle = area.Title;
        AreaOrder = area.Order;
        Name = method.Name;
        Title = @case.Title;
        Difference = method.GetCustomAttribute<DifferenceAttribute>()?.Reason;
    }

    /// <summary>The case class, which is also the name of its file in <c>Cases/</c>.</summary>
    public string Area { get; }

    public string AreaTitle { get; }

    public int AreaOrder { get; }

    public string Name { get; }

    public string Id => Area + "." + Name;

    public string Title { get; }

    /// <summary>The recorded reason for a difference, or null when the case must agree with NSubstitute.</summary>
    public string? Difference { get; }

    /// <summary>Every case in <paramref name="assembly"/>, in area order and then source order.</summary>
    public static IReadOnlyList<ComparisonCase> All(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<AreaAttribute>() is not null)
            .SelectMany(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttribute<CaseAttribute>() is not null)
                .OrderBy(method => method.MetadataToken)
                .Select(method => new ComparisonCase(method, type.GetCustomAttribute<AreaAttribute>()!, method.GetCustomAttribute<CaseAttribute>()!)))
            .OrderBy(@case => @case.AreaOrder)
            .ToList();

    /// <summary>
    /// Runs the case on a new thread, away from the test framework's synchronization context.
    /// NSubstitute keeps its last call per thread, so a thread that ran another case could hand that
    /// case's last call to this one. An exception that escapes the case is its last observation.
    /// </summary>
    public async Task<IReadOnlyList<Observation>> Run()
    {
        var observations = new Observations();
        try
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    if (_method.Invoke(null, [observations]) is Task task)
                    {
                        await task;
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
        }
        catch (Exception exception)
        {
            var thrown = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
            observations.Add(new Observation("the case", Observations.Thrown(thrown)));
        }

        return observations.Entries;
    }
}

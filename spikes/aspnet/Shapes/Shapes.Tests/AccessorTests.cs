#if SHOW_COMPILE_ERROR
extern alias internalapp;
#endif
extern alias ivtapp;

using System.Runtime.CompilerServices;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace Shapes.Tests;

/// <summary>
/// The fallback for an entry type that cannot know the shape: one accessor per shape, tried in turn.
/// An accessor whose method is missing throws MissingMethodException when it is called.
/// </summary>
public sealed class ProbingTaskIntAppEntry : IWebAppEntry
{
    private const string Program = "Program, TaskIntApp";

    public static string ApplicationName => "TaskIntApp";

    public static List<string> Misses { get; } = [];

    public static int Run(string[] args)
    {
        try
        {
            AsVoid(null, args);
            return 0;
        }
        catch (MissingMethodException miss)
        {
            Misses.Add(miss.Message);
        }

        try
        {
            return AsInt(null, args);
        }
        catch (MissingMethodException miss)
        {
            Misses.Add(miss.Message);
        }

        try
        {
            AsTask(null, args).GetAwaiter().GetResult();
            return 0;
        }
        catch (MissingMethodException miss)
        {
            Misses.Add(miss.Message);
        }

        return AsTaskOfInt(null, args).GetAwaiter().GetResult();
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "<Main>$")]
    private static extern void AsVoid([UnsafeAccessorType(Program)] object? type, string[] args);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "<Main>$")]
    private static extern int AsInt([UnsafeAccessorType(Program)] object? type, string[] args);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "<Main>$")]
    private static extern Task AsTask([UnsafeAccessorType(Program)] object? type, string[] args);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "<Main>$")]
    private static extern Task<int> AsTaskOfInt([UnsafeAccessorType(Program)] object? type, string[] args);
}

/// <summary>The hand-written form from the first spike, which names Program. An internal Program needs InternalsVisibleTo for it.</summary>
public sealed class TypedInternalIvtAppEntry : IWebAppEntry
{
    public static string ApplicationName => "InternalIvtApp";

    public static int Run(string[] args)
    {
        EntryPoint(null, args);
        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "<Main>$")]
    private static extern void EntryPoint(ivtapp::Program? program, string[] args);
}

[WebApp<ProbingTaskIntAppEntry>]
public partial class ProbingEntryTests
{
    [VerifyTest]
    public async Task Picks_the_accessor_that_exists_at_run_time(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(Day.Mocked);

        Verify(await client.GetStringAsync("/now") == "TaskIntApp 2026-11-17T00:00:00.0000000");
        Verify(ProbingTaskIntAppEntry.Misses.Count == 3);
        Console.WriteLine(string.Join(Environment.NewLine, ProbingTaskIntAppEntry.Misses));
    }
}

[WebApp<TypedInternalIvtAppEntry>]
public partial class TypedEntryTests
{
    [VerifyTest]
    public async Task Names_an_internal_Program_through_InternalsVisibleTo(HttpClient client, [Mock] IClock clock)
    {
        clock.Now.Returns(Day.Mocked);

        Verify(await client.GetStringAsync("/now") == "InternalIvtApp 2026-11-17T00:00:00.0000000");
    }
}

#if SHOW_COMPILE_ERROR
public sealed class TypedInternalAppEntry
{
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "<Main>$")]
    private static extern void EntryPoint(internalapp::Program? program, string[] args);
}
#endif

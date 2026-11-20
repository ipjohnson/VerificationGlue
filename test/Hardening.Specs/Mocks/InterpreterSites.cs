using VerificationGlue.Mocks;

namespace Hardening.Specs.Mocks;

#pragma warning disable VGM0005 // Bind runs Returns or Throws for the mock call that Call made just before it in the same flow.

/// <summary>The call sites of the interpreter: one per member and matcher pattern, in each setup style.</summary>
public sealed partial class Interpreter
{
    /// <summary>
    /// Makes the mock call of a site on <paramref name="receiver"/>. A matcher is written directly as
    /// an argument, so its interceptor applies. Each exact argument goes through <see cref="X"/> or
    /// <see cref="XS"/>, which run the scheduler's hook. TryGet gives its result and out value as a
    /// tuple, and Swap gives its ref value.
    /// </summary>
    private static object? Call(object receiver, Site site)
    {
        var a = site.Args;
        var m = site.Member;
        switch (m)
        {
            case Member.Get2:
                var get = (IService)receiver;
                return (a[0].Kind, a[1].Kind) switch
                {
                    (ArgKind.Exact, ArgKind.Exact) => get.Get(X(m, a[0]), X(m, a[1])),
                    (ArgKind.Exact, ArgKind.Any) => get.Get(X(m, a[0]), Arg.Any<int>()),
                    (ArgKind.Exact, ArgKind.Pred) => get.Get(X(m, a[0]), Arg.Is<int>(v => Inputs.Holds(a[1].Pred, v))),
                    (ArgKind.Any, ArgKind.Exact) => get.Get(Arg.Any<int>(), X(m, a[1])),
                    (ArgKind.Any, ArgKind.Any) => get.Get(Arg.Any<int>(), Arg.Any<int>()),
                    (ArgKind.Any, ArgKind.Pred) => get.Get(Arg.Any<int>(), Arg.Is<int>(v => Inputs.Holds(a[1].Pred, v))),
                    (ArgKind.Pred, ArgKind.Exact) => get.Get(Arg.Is<int>(v => Inputs.Holds(a[0].Pred, v)), X(m, a[1])),
                    (ArgKind.Pred, ArgKind.Any) => get.Get(Arg.Is<int>(v => Inputs.Holds(a[0].Pred, v)), Arg.Any<int>()),
                    (ArgKind.Pred, ArgKind.Pred) => get.Get(Arg.Is<int>(v => Inputs.Holds(a[0].Pred, v)), Arg.Is<int>(v => Inputs.Holds(a[1].Pred, v))),
                    _ => throw Unsupported(site),
                };
            case Member.Get1:
                var get1 = (IService)receiver;
                return a[0].Kind switch
                {
                    ArgKind.Exact => get1.Get(X(m, a[0])),
                    ArgKind.Any => get1.Get(Arg.Any<int>()),
                    ArgKind.Pred => get1.Get(Arg.Is<int>(v => Inputs.Holds(a[0].Pred, v))),
                    _ => get1.Get(Arg.Is((int)Inputs.Parse(m, a[0].Value)!)),
                };
            case Member.Name:
                var name = (IService)receiver;
                return a[0].Kind switch
                {
                    ArgKind.Exact => name.Name(XS(a[0])),
                    ArgKind.Any => name.Name(Arg.Any<string?>()),
                    ArgKind.Pred => name.Name(Arg.Is<string?>(v => Inputs.Holds(a[0].Pred, v))),
                    _ => name.Name(Arg.Is<string?>(a[0].Value)),
                };
            case Member.EchoInt:
                var echo = (IService)receiver;
                return a[0].Kind == ArgKind.Exact ? echo.Echo(X(m, a[0])) : a[0].Kind == ArgKind.Any ? echo.Echo(Arg.Any<int>()) : throw Unsupported(site);
            case Member.EchoString:
                var echoString = (IService)receiver;
                return a[0].Kind == ArgKind.Exact ? echoString.Echo(XS(a[0])) : a[0].Kind == ArgKind.Any ? echoString.Echo(Arg.Any<string?>()) : throw Unsupported(site);
            case Member.Index:
                var index = (IService)receiver;
                return a[0].Kind == ArgKind.Exact ? index[X(m, a[0])] : a[0].Kind == ArgKind.Any ? index[Arg.Any<int>()] : throw Unsupported(site);
            case Member.Count:
                return ((IService)receiver).Count;
            case Member.Load:
                var load = (IService)receiver;
                return a[0].Kind == ArgKind.Exact ? load.LoadAsync(X(m, a[0])) : a[0].Kind == ArgKind.Any ? load.LoadAsync(Arg.Any<int>()) : throw Unsupported(site);
            case Member.Peek:
                var peek = (IService)receiver;
                return a[0].Kind == ArgKind.Exact ? peek.PeekAsync(X(m, a[0])) : a[0].Kind == ArgKind.Any ? peek.PeekAsync(Arg.Any<int>()) : throw Unsupported(site);
            case Member.Reset:
                var reset = (IService)receiver;
                if (a[0].Kind == ArgKind.Exact)
                {
                    reset.Reset(X(m, a[0]));
                }
                else
                {
                    reset.Reset(Arg.Any<int>());
                }

                return null;
            case Member.TryGet:
                var tryGet = (IService)receiver;
                switch (a[0].Kind, a[1].Kind)
                {
                    case (ArgKind.Exact, ArgKind.Exact):
                        var found = tryGet.TryGet(X(m, a[0]), out var value);
                        return (found, value);
                    case (ArgKind.Exact, ArgKind.Any):
                        return tryGet.TryGet(X(m, a[0]), out Arg.Any<int>());
                    case (ArgKind.Any, ArgKind.Any):
                        return tryGet.TryGet(Arg.Any<int>(), out Arg.Any<int>());
                    default:
                        throw Unsupported(site);
                }

            case Member.Swap:
                var swap = (IService)receiver;
                if (a[0].Kind == ArgKind.Exact)
                {
                    var swapped = X(m, a[0]);
                    swap.Swap(ref swapped);
                    return swapped;
                }

                swap.Swap(ref Arg.Any<int>());
                return null;
            case Member.Child:
                var child = (IService)receiver;
                return a[0].Kind == ArgKind.Exact ? child.Child(X(m, a[0])) : a[0].Kind == ArgKind.Any ? child.Child(Arg.Any<int>()) : throw Unsupported(site);
            case Member.ChildValue:
                var childValue = (IChild)receiver;
                return a[0].Kind == ArgKind.Exact ? childValue.Value(X(m, a[0])) : a[0].Kind == ArgKind.Any ? childValue.Value(Arg.Any<int>()) : throw Unsupported(site);
            case Member.ChildLeaf:
                return ((IChild)receiver).Leaf;
            case Member.LeafValue:
                var leafValue = (ILeaf)receiver;
                return a[0].Kind == ArgKind.Exact ? leafValue.Value(X(m, a[0])) : a[0].Kind == ArgKind.Any ? leafValue.Value(Arg.Any<int>()) : throw Unsupported(site);
            case Member.LeafNext:
                return ((ILeaf)receiver).Next;
            default:
                throw Unsupported(site);
        }
    }

    private static int X(Member member, ArgSpec arg)
    {
        s_hook.Value?.Tick();
        return (int)Inputs.Parse(member, arg.Value)!;
    }

    private static string? XS(ArgSpec arg)
    {
        s_hook.Value?.Tick();
        return arg.Value;
    }

    private static HarnessException Unsupported(Site site) => new($"The interpreter has no call site for {site}.");

    /// <summary><c>Returns</c> or <c>Throws</c> for the mock call this flow made last, in the NSubstitute style.</summary>
    private string Bind(Site site, Response response)
    {
        var m = site.Member;
        if (response.Kind == ResponseKind.Throws)
        {
            MockSyntax.Throws(0, Inputs.Error(response.Id));
            return "set up";
        }

        switch (Inputs.ResultOf(m))
        {
            case Inputs.Result.Int:
                Returns(0, response, values => values.Select(value => (int)Inputs.ParseResult(m, value)!).ToArray(), info => (int)Factory(m, info)!);
                break;
            case Inputs.Result.String:
                Returns<string?>("", response, values => [.. values], info => (string?)Factory(m, info));
                break;
            case Inputs.Result.Bool:
                Returns(false, response, values => values.Select(value => (bool)Inputs.ParseResult(m, value)!).ToArray(), info => (bool)Factory(m, info)!);
                break;
            case Inputs.Result.Task:
                switch (response.Kind)
                {
                    case ResponseKind.Value:
                        Task.FromResult(0).Returns((int)Inputs.ParseResult(m, response.Values[0])!);
                        break;
                    case ResponseKind.Faulted:
                        MockSyntax.Returns(Task.FromResult(0), Task.FromException<int>(Inputs.Error(response.Id)));
                        break;
                    case ResponseKind.Cancelled:
                        MockSyntax.Returns(Task.FromResult(0), Task.FromCanceled<int>(new CancellationToken(true)));
                        break;
                    case ResponseKind.Sequence:
                        var tasks = response.Values.Select(value => Task.FromResult((int)Inputs.ParseResult(m, value)!)).ToArray();
                        MockSyntax.Returns(Task.FromResult(0), tasks[0], tasks[1..]);
                        break;
                    default:
                        MockSyntax.Returns(Task.FromResult(0), info => Task.FromResult((int)Factory(m, info)!));
                        break;
                }

                break;
            case Inputs.Result.ValueTask:
                switch (response.Kind)
                {
                    case ResponseKind.Value:
                        new ValueTask<int>(0).Returns((int)Inputs.ParseResult(m, response.Values[0])!);
                        break;
                    case ResponseKind.Faulted:
                        MockSyntax.Returns(new ValueTask<int>(0), ValueTask.FromException<int>(Inputs.Error(response.Id)));
                        break;
                    case ResponseKind.Cancelled:
                        MockSyntax.Returns(new ValueTask<int>(0), ValueTask.FromCanceled<int>(new CancellationToken(true)));
                        break;
                    default:
                        MockSyntax.Returns(new ValueTask<int>(0), info => new ValueTask<int>((int)Factory(m, info)!));
                        break;
                }

                break;
            case Inputs.Result.Child:
                MockSyntax.Returns((IChild)null!, (IChild)_slots.GetValueOrDefault(response.Id)!);
                break;
            case Inputs.Result.Leaf:
                MockSyntax.Returns((ILeaf)null!, (ILeaf)_slots.GetValueOrDefault(response.Id)!);
                break;
            default:
                throw new HarnessException($"A setup of {site} cannot return {response}.");
        }

        return "set up";
    }

    private static void Returns<T>(T call, Response response, Func<IReadOnlyList<string?>, T[]> values, Func<CallInfo, T> factory)
    {
        switch (response.Kind)
        {
            case ResponseKind.Value:
                call.Returns(values(response.Values)[0]);
                break;
            case ResponseKind.Sequence:
                var all = values(response.Values);
                call.Returns(all[0], all[1..]);
                break;
            case ResponseKind.Factory:
                call.Returns(factory);
                break;
            default:
                throw new HarnessException($"An NSubstitute-style setup cannot return {response}.");
        }
    }

    /// <summary>Runs <see cref="Inputs.Factory"/> on the arguments of a call, and writes back the out argument it sets.</summary>
    private static object? Factory(Member member, CallInfo info)
    {
        var arguments = Enumerable.Range(0, info.Count).Select(i => info[i]).ToArray();
        var result = Inputs.Factory(member, arguments);
        for (var i = 0; i < arguments.Length; i++)
        {
            info[i] = arguments[i];
        }

        return result;
    }

    private string SetUpTyped(object mock, SetupTyped typed)
    {
        var site = typed.Site;
        var a = site.Args;
        var m = site.Member;
        switch (m)
        {
            case Member.Get2:
                Typed(((IService)mock).Setup().Get(TI(m, a[0]), TI(m, a[1])), typed, IntValues(m), info => (int)Factory(m, info)!);
                break;
            case Member.Get1:
                Typed(((IService)mock).Setup().Get(TI(m, a[0])), typed, IntValues(m), info => (int)Factory(m, info)!);
                break;
            case Member.Name:
                Typed(((IService)mock).Setup().Name(TS(a[0])), typed, values => [.. values], info => (string?)Factory(m, info));
                break;
            case Member.EchoInt:
                Typed(((IService)mock).Setup().Echo(TI(m, a[0])), typed, IntValues(m), info => (int)Factory(m, info)!);
                break;
            case Member.EchoString:
                Typed(((IService)mock).Setup().Echo(TS(a[0])), typed, values => [.. values], info => (string?)Factory(m, info));
                break;
            case Member.Index:
                Typed(((IService)mock).Setup()[TI(m, a[0])], typed, IntValues(m), info => (int)Factory(m, info)!);
                break;
            case Member.Count:
                Typed(((IService)mock).Setup().Count, typed, IntValues(m), info => (int)Factory(m, info)!);
                break;
            case Member.Load:
                var load = ((IService)mock).Setup().LoadAsync(TI(m, a[0]));
                TypedAsync(load, typed, value => load.Returns(value), Task.FromException<int>, () => Task.FromCanceled<int>(new CancellationToken(true)), info => Task.FromResult((int)Factory(m, info)!));
                break;
            case Member.Peek:
                var peek = ((IService)mock).Setup().PeekAsync(TI(m, a[0]));
                TypedAsync(peek, typed, value => peek.Returns(value), ValueTask.FromException<int>, () => ValueTask.FromCanceled<int>(new CancellationToken(true)), info => new ValueTask<int>((int)Factory(m, info)!));
                break;
            case Member.Reset:
                TypedVoid(((IService)mock).Setup().Reset(TI(m, a[0])), typed);
                break;
            case Member.TryGet:
                Typed(((IService)mock).Setup().TryGet(TI(m, a[0]), TI(m, a[1])), typed, values => values.Select(value => (bool)Inputs.ParseResult(m, value)!).ToArray(), info => (bool)Factory(m, info)!);
                break;
            case Member.Swap:
                TypedVoid(((IService)mock).Setup().Swap(TI(m, a[0])), typed);
                break;
            case Member.Child:
                Typed(((IService)mock).Setup().Child(TI(m, a[0])), typed, _ => [(IChild)_slots.GetValueOrDefault(typed.Response!.Id)!], _ => throw Unsupported(site));
                break;
            case Member.ChildValue:
                Typed(((IChild)mock).Setup().Value(TI(m, a[0])), typed, IntValues(m), info => (int)Factory(m, info)!);
                break;
            case Member.ChildLeaf:
                Typed(((IChild)mock).Setup().Leaf, typed, _ => [(ILeaf)_slots.GetValueOrDefault(typed.Response!.Id)!], _ => throw Unsupported(site));
                break;
            case Member.LeafValue:
                Typed(((ILeaf)mock).Setup().Value(TI(m, a[0])), typed, IntValues(m), info => (int)Factory(m, info)!);
                break;
            default:
                throw Unsupported(site);
        }

        return "set up";
    }

    private static Func<IReadOnlyList<string?>, int[]> IntValues(Member member) =>
        values => values.Select(value => (int)Inputs.ParseResult(member, value)!).ToArray();

    private static void Typed<T>(MockSetup<T> setup, SetupTyped typed, Func<IReadOnlyList<string?>, T[]> values, Func<CallInfo, T> factory)
    {
        if (typed.Does is { } does)
        {
            setup.Does(info => Run(does, info));
        }

        switch (typed.Response?.Kind)
        {
            case null:
                break;
            case ResponseKind.Throws:
                setup.Throws(Inputs.Error(typed.Response.Id));
                break;
            case ResponseKind.Value or ResponseKind.SameAs:
                setup.Returns(values(typed.Response.Values)[0]);
                break;
            case ResponseKind.Sequence:
                var all = values(typed.Response.Values);
                setup.Returns(all[0], all[1..]);
                break;
            case ResponseKind.Factory:
                setup.Returns(factory);
                break;
            default:
                throw new HarnessException($"A typed setup cannot return {typed.Response}.");
        }
    }

    private static void TypedAsync<T>(MockSetup<T> setup, SetupTyped typed, Action<int> value, Func<Exception, T> faulted, Func<T> cancelled, Func<CallInfo, T> factory)
    {
        if (typed.Does is { } does)
        {
            setup.Does(info => Run(does, info));
        }

        switch (typed.Response?.Kind)
        {
            case null:
                break;
            case ResponseKind.Throws:
                setup.Throws(Inputs.Error(typed.Response.Id));
                break;
            case ResponseKind.Value:
                value((int)Inputs.ParseResult(Member.Load, typed.Response.Values[0])!);
                break;
            case ResponseKind.Faulted:
                setup.Returns(faulted(Inputs.Error(typed.Response.Id)));
                break;
            case ResponseKind.Cancelled:
                setup.Returns(cancelled());
                break;
            case ResponseKind.Factory:
                setup.Returns(factory);
                break;
            default:
                throw new HarnessException($"A typed async setup cannot return {typed.Response}.");
        }
    }

    private static void TypedVoid(MockSetup setup, SetupTyped typed)
    {
        if (typed.Does is { } does)
        {
            setup.Does(info => Run(does, info));
        }

        if (typed.Response is { Kind: ResponseKind.Throws } response)
        {
            setup.Throws(Inputs.Error(response.Id));
        }
        else if (typed.Response is not null)
        {
            throw new HarnessException($"A void member cannot return {typed.Response}.");
        }
    }

    private static CallCount Count(object mock, Site site)
    {
        var a = site.Args;
        var m = site.Member;
        return m switch
        {
            Member.Get2 => ((IService)mock).Calls().Get(TI(m, a[0]), TI(m, a[1])),
            Member.Get1 => ((IService)mock).Calls().Get(TI(m, a[0])),
            Member.Name => ((IService)mock).Calls().Name(TS(a[0])),
            Member.EchoInt => ((IService)mock).Calls().Echo(TI(m, a[0])),
            Member.EchoString => ((IService)mock).Calls().Echo(TS(a[0])),
            Member.Index => ((IService)mock).Calls()[TI(m, a[0])],
            Member.Count => ((IService)mock).Calls().Count,
            Member.Load => ((IService)mock).Calls().LoadAsync(TI(m, a[0])),
            Member.Peek => ((IService)mock).Calls().PeekAsync(TI(m, a[0])),
            Member.Reset => ((IService)mock).Calls().Reset(TI(m, a[0])),
            Member.TryGet => ((IService)mock).Calls().TryGet(TI(m, a[0]), TI(m, a[1])),
            Member.Swap => ((IService)mock).Calls().Swap(TI(m, a[0])),
            Member.Child => ((IService)mock).Calls().Child(TI(m, a[0])),
            Member.ChildValue => ((IChild)mock).Calls().Value(TI(m, a[0])),
            Member.ChildLeaf => ((IChild)mock).Calls().Leaf,
            Member.LeafValue => ((ILeaf)mock).Calls().Value(TI(m, a[0])),
            Member.LeafNext => ((ILeaf)mock).Calls().Next,
            _ => throw Unsupported(site),
        };
    }

    /// <summary>A typed int argument: an exact value, <c>Arg&lt;int&gt;.Any()</c>, a predicate, or <c>Arg&lt;int&gt;.Is(value)</c>.</summary>
    private static Arg<int> TI(Member member, ArgSpec arg) => arg.Kind switch
    {
        ArgKind.Exact => (int)Inputs.Parse(member, arg.Value)!,
        ArgKind.Any => Arg<int>.Any(),
        ArgKind.Pred => Arg<int>.Is(v => Inputs.Holds(arg.Pred, v)),
        _ => Arg<int>.Is((int)Inputs.Parse(member, arg.Value)!),
    };

    private static Arg<string?> TS(ArgSpec arg) => arg.Kind switch
    {
        ArgKind.Exact => arg.Value,
        ArgKind.Any => Arg<string?>.Any(),
        ArgKind.Pred => Arg<string?>.Is(v => Inputs.Holds(arg.Pred, v)),
        _ => Arg<string?>.Is(arg.Value),
    };
}

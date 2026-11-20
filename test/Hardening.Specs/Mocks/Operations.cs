using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hardening.Specs.Mocks;

/// <summary>A member of <see cref="IService"/>, <see cref="IChild"/> or <see cref="ILeaf"/>.</summary>
public enum Member
{
    Get2,
    Get1,
    Name,
    EchoInt,
    EchoString,
    Index,
    Count,
    Load,
    Peek,
    Reset,
    TryGet,
    Swap,
    Child,
    ChildValue,
    ChildLeaf,
    LeafValue,
    LeafNext,
}

public enum ArgKind
{
    Exact,
    Any,
    Pred,
    IsValue,
}

public enum Pred
{
    Even,
    AboveOne,
    NullOrEmpty,
}

/// <summary>Where a statement runs another flow's statement: before its mock call, or between the mock call and <c>Returns</c>.</summary>
public enum Point
{
    BeforeCall,
    BeforeReturns,
}

public enum ResponseKind
{
    Value,
    Sequence,
    Factory,
    Throws,
    Faulted,
    Cancelled,
    SameAs,
}

/// <summary>One argument of a statement. <see cref="Value"/> is the text of an int, a string, or null.</summary>
public sealed record ArgSpec(ArgKind Kind, string? Value = null, Pred Pred = Pred.Even)
{
    public static ArgSpec Exact(string? value) => new(ArgKind.Exact, value);

    public static readonly ArgSpec Any = new(ArgKind.Any);

    public override string ToString() => Kind switch
    {
        ArgKind.Exact => Value is null ? "null" : Value,
        ArgKind.Any => "Any",
        ArgKind.Pred => "Is(" + Pred + ")",
        _ => "Is(" + (Value ?? "null") + ")",
    };
}

/// <summary>
/// A member and its arguments. <see cref="Target"/> is the root mock for a member of
/// <see cref="IService"/>, and the slot that holds the mock for a member of <see cref="IChild"/> or
/// <see cref="ILeaf"/>.
/// </summary>
public sealed record Site(Member Member, int Target, IReadOnlyList<ArgSpec> Args)
{
    [JsonIgnore]
    public bool OnSlot => Member is Member.ChildValue or Member.ChildLeaf or Member.LeafValue or Member.LeafNext;

    [JsonIgnore]
    public int Exacts => Args.Count(arg => arg.Kind == ArgKind.Exact);

    public override string ToString()
    {
        var receiver = OnSlot ? $"slot{Target}" : $"root{Target}";
        var args = string.Join(", ", Args);
        return Member switch
        {
            Member.Get2 or Member.Get1 => $"{receiver}.Get({args})",
            Member.EchoInt => $"{receiver}.Echo<int>({args})",
            Member.EchoString => $"{receiver}.Echo<string>({args})",
            Member.Index => $"{receiver}[{args}]",
            Member.Count => $"{receiver}.Count",
            Member.Load => $"{receiver}.LoadAsync({args})",
            Member.Peek => $"{receiver}.PeekAsync({args})",
            Member.ChildValue or Member.LeafValue => $"{receiver}.Value({args})",
            Member.ChildLeaf => $"{receiver}.Leaf",
            Member.LeafNext => $"{receiver}.Next",
            _ => $"{receiver}.{Member}({args})",
        };
    }
}

/// <summary>A response. <see cref="Values"/> holds one value, or a sequence. <see cref="Id"/> names a thrown exception or the slot of <see cref="ResponseKind.SameAs"/>.</summary>
public sealed record Response(ResponseKind Kind, IReadOnlyList<string?> Values, int Id = 0)
{
    public override string ToString() => Kind switch
    {
        ResponseKind.Value => Show(Values[0]),
        ResponseKind.Sequence => string.Join(", ", Values.Select(Show)),
        ResponseKind.Factory => "factory",
        ResponseKind.Throws => $"throw r{Id}",
        ResponseKind.Faulted => $"faulted r{Id}",
        ResponseKind.Cancelled => "cancelled",
        _ => $"slot{Id}",
    };

    private static string Show(string? value) => value is null ? "null" : value;
}

/// <summary>A callback. One that writes sets the call's last argument to 100 plus its ID, which a <c>ref</c> or <c>out</c> argument returns.</summary>
public sealed record Callback(int Id, bool Writes = false)
{
    public override string ToString() => Writes ? $"c{Id}(writes)" : $"c{Id}";
}

/// <summary>One operation of a sequence. Each is a whole statement unless it groups others.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(Invoke), "invoke")]
[JsonDerivedType(typeof(SetupNs), "setup")]
[JsonDerivedType(typeof(SetupTyped), "typed")]
[JsonDerivedType(typeof(When), "when")]
[JsonDerivedType(typeof(Check), "check")]
[JsonDerivedType(typeof(CountCalls), "calls")]
[JsonDerivedType(typeof(Snapshot), "snapshot")]
[JsonDerivedType(typeof(Fetch), "fetch")]
[JsonDerivedType(typeof(SetProperty), "set")]
[JsonDerivedType(typeof(Subscribe), "subscribe")]
[JsonDerivedType(typeof(Unsubscribe), "unsubscribe")]
[JsonDerivedType(typeof(RaiseEvent), "raise")]
[JsonDerivedType(typeof(Yield), "yield")]
[JsonDerivedType(typeof(ChildTask), "child")]
[JsonDerivedType(typeof(FinishInChild), "finish")]
[JsonDerivedType(typeof(AbandonInChild), "abandon")]
[JsonDerivedType(typeof(Interleave), "interleave")]
[JsonDerivedType(typeof(ParallelCalls), "parallel")]
[JsonDerivedType(typeof(ParallelSetups), "parallelSetups")]
public abstract record Op
{
    /// <summary>The ops this one runs inside it, for shrinking.</summary>
    [JsonIgnore]
    public virtual IReadOnlyList<Op> Inner => [];
}

/// <summary>A call with exact arguments.</summary>
public sealed record Invoke(Site Site) : Op
{
    public override string ToString() => Site + ";";
}

/// <summary>An NSubstitute-style setup, <c>site.Returns(...)</c>. With <see cref="Await"/>, the response value comes from an await.</summary>
public sealed record SetupNs(Site Site, Response Response, bool Await = false) : Op
{
    public override string ToString() => Response.Kind == ResponseKind.Throws
        ? $"{Site}.Throws(r{Response.Id});"
        : $"{Site}.Returns({(Await ? "await " : "")}{Response});";
}

/// <summary>A typed setup, <c>Setup().member(...)</c>, with an optional <c>Does</c> callback and an optional response.</summary>
public sealed record SetupTyped(Site Site, Response? Response, Callback? Does = null) : Op
{
    public override string ToString() =>
        $"{Site}.Setup(){(Does is null ? "" : $".Does({Does})")}{(Response is null ? "" : Response.Kind == ResponseKind.Throws ? $".Throws(r{Response.Id})" : $".Returns({Response})")};";
}

/// <summary><c>When(site).Do(callback)</c>, or <c>When(site).Throw(...)</c> when <see cref="Throw"/> is set.</summary>
public sealed record When(Site Site, Callback Callback, bool Throw = false) : Op
{
    public override string ToString() => Throw ? $"When({Site}).Throw(r{Callback.Id});" : $"When({Site}).Do({Callback});";
}

/// <summary><c>Received()</c>, <c>Received(n)</c> or <c>DidNotReceive()</c>, as a minimum and maximum count.</summary>
public sealed record Check(Site Site, int Min, int Max) : Op
{
    public override string ToString() => (Min, Max) switch
    {
        (0, 0) => $"DidNotReceive {Site};",
        (1, int.MaxValue) => $"Received {Site};",
        _ => $"Received({Min}) {Site};",
    };
}

/// <summary>A typed <c>Calls()</c> count.</summary>
public sealed record CountCalls(Site Site) : Op
{
    public override string ToString() => $"Calls {Site};";
}

/// <summary>The received calls of a root mock, or of the mock in a slot.</summary>
public sealed record Snapshot(int Target, bool Slot) : Op
{
    public override string ToString() => $"ReceivedCalls({(Slot ? "slot" : "root")}{Target});";
}

/// <summary>Stores the mock a <see cref="Member.Child"/> or <see cref="Member.ChildLeaf"/> call returns in a slot.</summary>
public sealed record Fetch(int Slot, Site Site) : Op
{
    public override string ToString() => $"slot{Slot} = {Site};";
}

public sealed record SetProperty(int Mock, bool Indexer, int Index, int Value) : Op
{
    public override string ToString() => Indexer ? $"root{Mock}[{Index}] = {Value};" : $"root{Mock}.Count = {Value};";
}

public sealed record Subscribe(int Mock, int Handler) : Op
{
    public override string ToString() => $"root{Mock}.Changed += h{Handler};";
}

public sealed record Unsubscribe(int Mock, int Handler) : Op
{
    public override string ToString() => $"root{Mock}.Changed -= h{Handler};";
}

public sealed record RaiseEvent(int Mock, int Value) : Op
{
    public override string ToString() => $"root{Mock}.Changed += Raise.Event({Value});";
}

/// <summary><c>await Task.Yield()</c> between statements.</summary>
public sealed record Yield : Op
{
    public override string ToString() => "await Task.Yield();";
}

/// <summary>Statements run in a child task that the parent awaits.</summary>
public sealed record ChildTask(IReadOnlyList<Op> Ops) : Op
{
    [JsonIgnore]
    public override IReadOnlyList<Op> Inner => Ops;

    public override string ToString() => $"await Task.Run(() => {{ {string.Join(" ", Ops)} }});";
}

/// <summary>The mock call of a setup in this flow, and its <c>Returns</c> in a child task.</summary>
public sealed record FinishInChild(SetupNs Setup) : Op
{
    public override string ToString() => $"var call = {Setup.Site}; await Task.Run(() => call.Returns({Setup.Response}));";
}

/// <summary>A child task that queues a matcher and makes no mock call.</summary>
public sealed record AbandonInChild : Op
{
    public override string ToString() => "await Task.Run(() => Arg.Any<int>());";
}

/// <summary>
/// Statement <see cref="A"/> in this flow, with statement <see cref="B"/> run by another flow at
/// <see cref="At"/>. With <see cref="Await"/>, A awaits B, so it can resume on another thread. Without
/// it, A blocks its thread until B is done.
/// </summary>
public sealed record Interleave(Op A, Point At, bool Await, Op B) : Op
{
    [JsonIgnore]
    public override IReadOnlyList<Op> Inner => [A, B];

    public override string ToString() => $"{{ {A} }} with {{ {B} }} in another flow {(At == Point.BeforeCall ? "before the mock call" : "before Returns")}{(Await ? ", awaited" : ", blocking")};";
}

/// <summary>Flows that start together behind a barrier and each run their calls.</summary>
public sealed record ParallelCalls(IReadOnlyList<IReadOnlyList<Op>> Branches) : Op
{
    [JsonIgnore]
    public override IReadOnlyList<Op> Inner => [.. Branches.SelectMany(branch => branch)];

    public override string ToString() => $"parallel {string.Join(" | ", Branches.Select(branch => "{ " + string.Join(" ", branch) + " }"))};";
}

/// <summary>Flows that start together behind a barrier and each run one setup on a call no other branch sets up.</summary>
public sealed record ParallelSetups(IReadOnlyList<Op> Branches) : Op
{
    [JsonIgnore]
    public override IReadOnlyList<Op> Inner => Branches;

    public override string ToString() => $"parallel setups {string.Join(" | ", Branches)};";
}

/// <summary>A generated sequence: its ops, run on two root mocks.</summary>
public sealed record Sequence(string Name, IReadOnlyList<Op> Ops)
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int Size => Ops.Sum(Weight);

    public string Json() => JsonSerializer.Serialize(Ops, s_json);

    public static Sequence FromJson(string name, string json) => new(name, JsonSerializer.Deserialize<List<Op>>(json, s_json)!);

    public override string ToString() => string.Join('\n', Ops.Select((op, i) => string.Create(CultureInfo.InvariantCulture, $"  {i,2}: {op}")));

    private static int Weight(Op op) => 1 + op.Inner.Sum(Weight);
}

using Glue.Mocks;

var a = Substitute.For<IHasStatic>();
var b = Substitute.For<SealedThing>();
T Helper<T>() where T : class => Substitute.For<T>();
Helper<IPlain>();

static class Tests { public static void Param([Mock] IHasStatic x) { } }

public interface IHasStatic { static abstract int Create(); int Value { get; } }
public interface IPlain { int Value { get; } }
public sealed class SealedThing { }

using VerificationGlue;
using VerificationGlue.Mocks;
using static VerificationGlue.VerifySyntax;

namespace Hardening.Specs.Interactions;

public sealed record Item(int Id);

/// <summary>A generic interface whose <see cref="Shelf"/> member returns a recursive mock of another generic interface.</summary>
public interface ICatalog<T>
{
    IShelf<T> Shelf(string name);

    Task<IShelf<T>> OpenAsync(string name);
}

/// <summary>Reached through <see cref="ICatalog{T}.Shelf"/>, with async members and a generic async method.</summary>
public interface IShelf<T>
{
    Task<T> FindAsync(int id);

    ValueTask<int> CountAsync();

    Task<TResult> MapAsync<TResult>(int id);
}

/// <summary>
/// MIX-004 of docs/design/hardening.md: the async members of a recursive mock of a generic type, set
/// up in the NSubstitute style and the typed style on one mock. The last matching setup wins whatever
/// its style (<c>CD-02</c>), the recursive mock and the returned values keep their identity
/// (<c>CD-03</c>), and setups and checks add no received calls.
/// </summary>
[Trait("Step", "5")]
public partial class RecursiveGenericAsyncSpecs
{
    [VerifyTest]
    public async Task Both_styles_set_up_the_async_members_of_a_generic_recursive_mock()
    {
        var catalog = Mock.Of<ICatalog<Item>>();
        var shelf = catalog.Shelf("a");
        Item one = new(1), fallback = new(0), three = new(3);

        shelf.FindAsync(1).Returns(one);
        shelf.Setup().FindAsync(Arg<int>.Any()).Returns(fallback);
        shelf.FindAsync(3).Returns(three);
        shelf.Setup().CountAsync().Returns(2);
        shelf.MapAsync<string>(1).Returns("one");
        shelf.Setup().MapAsync<string>(Arg<int>.Any()).Returns("any");
        shelf.MapAsync<int>(Arg.Any<int>()).Returns(7);

        Verify(ReferenceEquals(catalog.Shelf("a"), shelf));
        Verify(!ReferenceEquals(catalog.Shelf("b"), shelf));
        Verify(ReferenceEquals(await catalog.Shelf("a").FindAsync(1), fallback));
        Verify(ReferenceEquals(await shelf.FindAsync(3), three));
        Verify(ReferenceEquals(await shelf.FindAsync(4), fallback));
        Verify(await shelf.CountAsync() == 2);
        Verify(await shelf.MapAsync<string>(1) == "any");
        Verify(await shelf.MapAsync<string>(2) == "any");
        Verify(await shelf.MapAsync<int>(1) == 7);
        Verify(await catalog.Shelf("b").FindAsync(1) is null);

        Verify(shelf.Calls().FindAsync(Arg<int>.Any()) == 3);
        Verify(shelf.Calls().FindAsync(1) == 1);
        Verify(shelf.Calls().CountAsync() == 1);
        Verify(shelf.Calls().MapAsync<string>(Arg<int>.Any()) == 2);
        Verify(shelf.Calls().MapAsync<int>(Arg<int>.Any()) == 1);
        _ = shelf.Received(1).FindAsync(3);
        _ = shelf.DidNotReceive().FindAsync(2);
        Verify(MockAdapter.Of(shelf).ReceivedCalls().Count == 7);
        Verify(catalog.Calls().Shelf("a") == 3);
        Verify(catalog.Calls().Shelf("b") == 2);
        Verify(MockAdapter.Of(catalog).ReceivedCalls().Count == 5);
    }

    [VerifyTest]
    public async Task An_async_member_that_returns_the_generic_type_gives_the_instance_each_style_sets_up()
    {
        var catalog = Mock.Of<ICatalog<Item>>();
        var shelf = catalog.Shelf("a");
        var other = Mock.Of<ICatalog<Item>>().Shelf("a");

        Verify(await catalog.OpenAsync("a") is null);

        catalog.OpenAsync("a").Returns(shelf);
        var first = await catalog.OpenAsync("a");
        catalog.Setup().OpenAsync(Arg<string>.Any()).Returns(other);
        var second = await catalog.OpenAsync("a");
        catalog.OpenAsync("a").Returns(shelf);
        var third = await catalog.OpenAsync("a");

        Verify(ReferenceEquals(first, shelf));
        Verify(ReferenceEquals(second, other));
        Verify(ReferenceEquals(third, shelf));
        Verify(ReferenceEquals(await catalog.OpenAsync("b"), other));
        Verify(ReferenceEquals(catalog.Shelf("a"), shelf));

        third.Setup().FindAsync(5).Returns(new Item(5));
        Verify((await shelf.FindAsync(5)).Id == 5);
        Verify(catalog.Calls().OpenAsync("a") == 4);
        Verify(catalog.Calls().OpenAsync(Arg<string>.Any()) == 5);
        Verify(catalog.Calls().Shelf(Arg<string>.Any()) == 2);
        Verify(shelf.Calls().FindAsync(5) == 1);
        Verify(MockAdapter.Of(other).ReceivedCalls().Count == 0);
    }
}

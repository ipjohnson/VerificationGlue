using System.Collections;
using Shapes.Collisions;
using VerificationGlue.Mocks;

namespace Hardening.Specs.ShapeConsumers;

/// <summary>Collisions.cs: inherited interfaces, members with one name in two bases, overloads across bases and a diamond.</summary>
[Trait("Step", "3")]
public class CollisionConsumerSpecs
{
    [VerifyTest]
    public void Members_with_one_name_in_two_base_interfaces_are_set_up_and_checked_apart([Mock] IReadWriter parameter) =>
        Paths.Each(Mock.Of<IReadWriter>(), parameter, mock =>
        {
            IReader reader = mock;
            IWriter writer = mock;
            var value = new object();
            mock.Read().Returns(value);
            reader.Read().Returns("text");
            reader.Count.Returns(3);
            writer.Count.Returns(4L);
            var plain = 0;
            var numbered = 0;
            reader.Changed += (_, _) => plain++;
            writer.Changed += (_, number) => numbered += number;

            Assert.Same(value, mock.Read());
            Assert.Equal("text", reader.Read());
            writer.Read("written");
            Assert.Equal(3, reader.Count);
            Assert.Equal(4L, writer.Count);
            writer.Count = 9;
            reader.Changed += Raise.Event<EventHandler>(mock, EventArgs.Empty);
            writer.Changed += Raise.Event<EventHandler<int>>(mock, 5);

            Assert.Equal(1, plain);
            Assert.Equal(5, numbered);
            mock.Received(1).Read();
            reader.Received(1).Read();
            writer.Received(1).Read("written");
            writer.DidNotReceive().Read("other");
            Assert.True(mock.Calls().Read(Arg<string>.Any()) == 1);
            Assert.Equal(
                ["Read()", "Read()", "Read(written)", "get_Count()", "get_Count()", "set_Count(9)"],
                ((IMock)mock).Adapter.ReceivedCalls().Where(call => !call.MemberName.StartsWith("add_", StringComparison.Ordinal)).Select(call => call.ToString()));
        });

    [VerifyTest]
    public void A_diamond_with_two_default_bodies_for_one_member_is_mocked([Mock] IDiamond parameter) =>
        Paths.Each(Mock.Of<IDiamond>(), parameter, mock =>
        {
            IRoot root = mock;
            mock.Value.Returns(1);
            root.Value.Returns(2);
            var runs = 0;
            root.When(r => r.Run()).Do(_ => runs++);

            root.Run();

            Assert.Equal(1, mock.Value);
            Assert.Equal(2, root.Value);
            Assert.Equal(1, runs);
            root.Received(1).Run();
        });

    [VerifyTest]
    public void A_generic_interface_with_two_enumerable_bases_is_mocked([Mock] IDictionaryLike<string, int> parameter) =>
        Paths.Each(Mock.Of<IDictionaryLike<string, int>>(), parameter, mock =>
        {
            IEnumerable plain = mock;
            KeyValuePair<string, int>[] pairs = [new("a", 1), new("b", 2)];
            mock.GetEnumerator().Returns(_ => ((IEnumerable<KeyValuePair<string, int>>)pairs).GetEnumerator());
            plain.GetEnumerator().Returns(pairs.GetEnumerator());

            Assert.Equal(pairs, mock.ToArray());
            Assert.True(plain.GetEnumerator().MoveNext());

            mock.Received(1).GetEnumerator();
            plain.Received(1).GetEnumerator();
        });
}

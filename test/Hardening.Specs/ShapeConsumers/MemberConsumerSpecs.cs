using Shapes.Members;
using VerificationGlue.Mocks;

namespace Hardening.Specs.ShapeConsumers;

/// <summary>Members.cs: async returns, ref returns, events, default interface members and indexers.</summary>
[Trait("Step", "3")]
public class MemberConsumerSpecs
{
    [VerifyTest]
    public Task Async_members_return_what_was_set_up_and_completed_tasks_otherwise([Mock] IAsync parameter) =>
        Paths.EachAsync(Mock.Of<IAsync>(), parameter, async mock =>
        {
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();
            mock.Compute(2).Returns(4);
            mock.Read().Returns("read");
            mock.Generic("x").Returns("y");
            mock.Run(cancelled.Token).Returns(Task.FromCanceled(cancelled.Token));

            Assert.Equal(4, await mock.Compute(2));
            Assert.Equal(0, await mock.Compute(3));
            Assert.Equal("read", await mock.Read());
            Assert.Equal("y", await mock.Generic("x"));
            await mock.Flush();
            await mock.Run(CancellationToken.None);
            await Assert.ThrowsAsync<TaskCanceledException>(() => mock.Run(cancelled.Token));

            await mock.Received(1).Compute(2);
            await mock.Received(1).Flush();
            await mock.Received(2).Run(Arg.Any<CancellationToken>());
            Assert.True(mock.Calls().Generic<string>("x") == 1);
            Assert.True(mock.Calls().Stream() == 0);
        });

    [VerifyTest]
    public void Ref_returns_give_the_value_set_up([Mock] IRefReturns parameter) =>
        Paths.Each(Mock.Of<IRefReturns>(), parameter, mock =>
        {
            var id = Guid.NewGuid();
            mock.Setup().Slot(1).Returns(5);
            mock.Setup().ReadonlySlot().Returns(id);
            mock.Setup().GenericSlot<string>().Returns("slot");
            mock.Setup()[2].Returns(6);
            mock.Setup().Total.Returns(7L);
            mock.Setup().Write(Arg<int>.Any()).Returns(info =>
            {
                info[0] = 3;
                return 8;
            });

            Assert.Equal(5, mock.Slot(1));
            Assert.Equal(0, mock.Slot(9));
            Assert.Equal(id, mock.ReadonlySlot());
            Assert.Equal("slot", mock.GenericSlot<string>());
            Assert.Equal(6, mock[2]);
            Assert.Equal(7L, mock.Total);
            Assert.Equal(8, mock.Write(out var written));
            Assert.Equal(3, written);

            Assert.True(mock.Calls().Slot(Arg<int>.Any()) == 2);
            Assert.True(mock.Calls()[2] == 1);
            mock.Received(1).Write(out _);
        });

    [VerifyTest]
    public void Each_event_shape_is_raised_with_its_arguments([Mock] IEvents parameter) =>
        Paths.Each(Mock.Of<IEvents>(), parameter, mock =>
        {
            var seen = new List<string>();
            EventHandler plain = (_, _) => seen.Add("plain");
            mock.Plain += plain;
            mock.Generic += (_, number) => seen.Add("generic " + number);
            mock.ByRef += (ref int count, out string label, in DateTime at) =>
            {
                count++;
                label = "labelled";
                seen.Add("by ref " + at.Year);
                return count;
            };
            mock.Many += (text, number, item) => seen.Add($"many {text} {number} {item}");
            mock.Returning += number => Task.FromResult(number > 0);

            mock.Plain += Raise.Event<EventHandler>(mock, EventArgs.Empty);
            mock.Generic += Raise.Event<EventHandler<int>>(mock, 2);
            mock.ByRef += Raise.Event<Counter>(1, "", new DateTime(2026, 11, 20));
            mock.Many += Raise.Event<Action<string, int, object?>>("a", 3, null);
            mock.Returning += Raise.Event<Func<int, Task<bool>>>(4);
            mock.Plain -= plain;
            mock.Plain += Raise.Event<EventHandler>(mock, EventArgs.Empty);

            Assert.Equal(["plain", "generic 2", "by ref 2026", "many a 3 "], seen);
            Assert.Equal(
                ["add_Plain", "add_Generic", "add_ByRef", "add_Many", "add_Returning", "remove_Plain"],
                ((IMock)mock).Adapter.ReceivedCalls().Select(call => call.MemberName).Where(name => !name.StartsWith("raise", StringComparison.Ordinal)).Distinct());
        });

    [VerifyTest]
    public void Default_members_are_mocked_and_sealed_and_static_members_keep_their_bodies([Mock] IDefaults parameter) =>
        Paths.Each(Mock.Of<IDefaults>(), parameter, mock =>
        {
            mock.Abstract().Returns(10);
            mock.Property.Returns(11);
            EventHandler handler = (_, _) => { };

            Assert.Equal(10, mock.Abstract());
            Assert.Equal(0, mock.Virtual());
            Assert.Equal(11, mock.Property);
            Assert.Equal(2, mock.Sealed());
            Assert.Equal(4, IDefaults.Static());
            mock.Property = 12;
            mock.Event += handler;

            mock.Received(1).Virtual();
            mock.Received(1).Property = 12;
            Assert.True(mock.Calls().Abstract() == 1);
            Assert.Equal(
                ["Abstract()", "Virtual()", "get_Property()", "set_Property(12)", "add_Event"],
                ((IMock)mock).Adapter.ReceivedCalls().Select(call => call.MemberName.StartsWith("add_", StringComparison.Ordinal) ? call.MemberName : call.ToString()));
        });

    [VerifyTest]
    public void Indexers_with_one_or_two_keys_and_a_set_only_indexer_are_mocked([Mock] IIndexers parameter) =>
        Paths.Each(Mock.Of<IIndexers>(), parameter, mock =>
        {
            var id = Guid.NewGuid();
            mock[1].Returns("one");
            mock.Setup()["key", Arg<int>.Any()].Returns("keyed");

            Assert.Equal("one", mock[1]);
            Assert.Equal("", mock[2]);
            Assert.Equal("keyed", mock["key", 3]);
            mock[1] = "set";
            mock[id] = 4;

            mock.Received(1)[id] = 4;
            mock.Received(1)[1] = "set";
            mock.DidNotReceive()[id] = 5;
            Assert.True(mock.Calls()[Arg<int>.Any()] == 2);
            Assert.True(mock.Calls()["key", 3] == 1);
        });
}

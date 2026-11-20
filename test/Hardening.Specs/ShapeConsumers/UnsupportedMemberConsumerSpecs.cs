using Shapes.Pointers;
using Shapes.RefStruct;
using VerificationGlue.Mocks;

namespace Hardening.Specs.ShapeConsumers;

// Each [Mock] parameter and Mock.Of<T>() call below reports VGM0003 for every member that throws.
#pragma warning disable VGM0003

/// <summary>
/// RefStruct.cs and Pointers.cs: members with a ref struct or pointer in their signature. They report
/// VGM0003 where the mock is named, and calling one throws NotSupportedException (SHAPE-007). An event
/// whose delegate takes a ref struct accepts handlers, and raising it throws (SHAPE-008).
/// </summary>
[Trait("Step", "3")]
public unsafe class UnsupportedMemberConsumerSpecs
{
    [VerifyTest]
    public void Ref_struct_members_throw_and_the_event_accepts_handlers_but_cannot_be_raised([Mock] ISpans parameter) =>
        Paths.Each(Mock.Of<ISpans>(), parameter, mock =>
        {
            var handled = 0;
            SpanHandler handler = _ => handled++;

            Assert.Equal(
                "Parse cannot be mocked: parameter 'text' is the ref struct 'System.ReadOnlySpan<char>'.",
                Assert.Throws<NotSupportedException>(() => mock.Parse("1")).Message);
            Assert.Throws<NotSupportedException>(() => mock.Buffer(4));
            Assert.Throws<NotSupportedException>(() => mock.TryWrite(new char[2], out _));
            Assert.Throws<NotSupportedException>(() => mock.Values.Length);
            Assert.Throws<NotSupportedException>(() => mock[new int[1]]);
            mock.Text += handler;
            var raise = Assert.Throws<NotSupportedException>(() => mock.Text += Raise.Event<SpanHandler>("text"));
            mock.Text -= handler;
            mock.Text += handler;

            Assert.Equal("Text cannot be raised, because its delegate takes a ref struct or a pointer.", raise.Message);
            Assert.Equal(0, handled);

            // The raise is not an added handler, and the add after it is an ordinary add.
            Assert.Equal(
                ["add_Text", "remove_Text", "add_Text"],
                ((IMock)mock).Adapter.ReceivedCalls().Select(call => call.MemberName));
        });

    [VerifyTest]
    public void Pointer_members_throw_and_the_other_member_is_set_up_and_checked([Mock] IPointers parameter) =>
        Paths.Each(Mock.Of<IPointers>(), parameter, mock =>
        {
            var buffer = stackalloc byte[4];
            mock.Safe(1).Returns(2);

            Assert.Throws<NotSupportedException>(() => mock.Allocate(4));
            Assert.Throws<NotSupportedException>(() => mock.Read(buffer, 4));
            Assert.Throws<NotSupportedException>(() => mock.Table);
            Assert.Throws<NotSupportedException>(() => (nint)mock.Callback);
            Assert.Equal(2, mock.Safe(1));
            Assert.Equal(0, mock.Safe(3));

            mock.Received(1).Safe(1);
            Assert.True(mock.Calls().Safe(Arg<int>.Any()) == 2);
            Assert.Equal(["Safe(1)", "Safe(3)"], ((IMock)mock).Adapter.ReceivedCalls().Select(call => call.ToString()));
        });
}

using Shapes.Names;
using VerificationGlue.Mocks;

namespace Hardening.Specs.ShapeConsumers;

/// <summary>Names.cs: keyword identifiers, names the mock uses for its locals, init and set-only accessors, and hiding with new.</summary>
[Trait("Step", "3")]
public class NameConsumerSpecs
{
    [VerifyTest]
    public void Members_named_with_keywords_are_mocked([Mock] @interface parameter) =>
        Paths.Each(Mock.Of<@interface>(), parameter, mock =>
        {
            mock.@namespace.Returns(3);
            var raised = 0;
            mock.@delegate += () => raised++;

            mock.@class(1, "two");
            mock.@delegate += Raise.Event<Action>();

            Assert.Equal(3, mock.@namespace);
            Assert.Equal(1, raised);
            mock.Received(1).@class(1, "two");
            Assert.True(mock.Calls().@class(Arg<int>.Any(), "two") == 1);
        });

    [VerifyTest]
    public void Parameters_named_like_the_mock_locals_keep_their_values([Mock] IContextual parameter) =>
        Paths.Each(Mock.Of<IContextual>(), parameter, mock =>
        {
            mock.field.Returns(2);
            mock.var(1, 2, 3, 4, 5).Returns(info => info.ArgAt<int>(0) + info.ArgAt<int>(4));

            mock.value(7);
            mock.Locals(1, 2, 3, 4, 5);

            Assert.Equal(2, mock.field);
            Assert.Equal(6, mock.var(1, 2, 3, 4, 5));
            mock.Received(1).value(7);
            mock.Received(1).Locals(1, 2, 3, 4, 5);
            mock.DidNotReceive().Locals(5, 4, 3, 2, 1);
        });

    [VerifyTest]
    public void Init_set_only_and_get_only_accessors_are_mocked([Mock] IAccessors parameter) =>
        Paths.Each(Mock.Of<IAccessors>(), parameter, mock =>
        {
            mock.Init.Returns(1);
            mock.GetOnly.Returns(2);

            mock.SetOnly = "set";
            mock["key"] = 3;

            Assert.Equal(1, mock.Init);
            Assert.Equal(2, mock.GetOnly);
            mock.Received(1).SetOnly = "set";
            mock.Received(1)["key"] = 3;
            Assert.True(mock.Calls().GetOnly == 1);
        });

    [VerifyTest]
    public void A_member_hidden_with_new_and_the_member_it_hides_are_set_up_apart([Mock] IHiding parameter) =>
        Paths.Each(Mock.Of<IHiding>(), parameter, mock =>
        {
            IBase hidden = mock;
            mock.Value.Returns("new");
            hidden.Value.Returns(1);
            mock.Name().Returns(2);
            hidden.Name().Returns("base");

            mock.Run();

            Assert.Equal("new", mock.Value);
            Assert.Equal(1, hidden.Value);
            Assert.Equal(2, mock.Name());
            Assert.Equal("base", hidden.Name());
            mock.Received(1).Run();
            hidden.DidNotReceive().Run();
            mock.Value = "set";
            mock.Received(1).Value = "set";
        });
}

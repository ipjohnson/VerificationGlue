using Shapes.Nullable;
using VerificationGlue.Mocks;

namespace Hardening.Specs.ShapeConsumers;

/// <summary>Nullable.cs: nullable annotations, tuples with and without names, and a generic interface closed over each kind of type.</summary>
[Trait("Step", "3")]
public class NullableConsumerSpecs
{
    [VerifyTest]
    public void Nullable_members_take_and_return_null([Mock] INullable parameter) =>
        Paths.Each(Mock.Of<INullable>(), parameter, mock =>
        {
            mock.Name.Returns((string?)null);
            mock.Names(Arg.Any<Dictionary<string, List<int?>?>>()).Returns([null, "b"]);
            mock.Count(null, null, null, null).Returns((int?)null);
            mock.Count(Arg.Is<int?[]?>(counts => counts != null), null, null, null).Returns(1);
            string? changed = "unset";
            mock.Changed += (_, text) => changed = text;

            mock.Changed += Raise.Event<EventHandler<string?>>(mock, null);

            Assert.Null(mock.Name);
            Assert.Equal([null, "b"], mock.Names(new() { ["a"] = null }));
            Assert.Null(mock.Count(null, null, null, null));
            Assert.Equal(1, mock.Count([null], null, null, null));
            Assert.Null(changed);
            mock.Received(2).Count(Arg.Any<int?[]?>(), null, null, null);
            Assert.True(mock.Calls().Names(Arg<Dictionary<string, List<int?>?>>.Any()) == 1);
        });

    [VerifyTest]
    public void Tuples_with_names_one_element_and_nine_elements_are_returned([Mock] ITuples parameter) =>
        Paths.Each(Mock.Of<ITuples>(), parameter, mock =>
        {
            mock.Pair().Returns((1, "one"));
            mock.Nested(Arg.Any<(int X, int Y)[]>()).Returns(info => (info.ArgAt<(int X, int Y)[]>(0).Length, ("Ada", "Lovelace")));
            mock.Single().Returns(new ValueTuple<int>(5));
            mock.Long().Returns((1, 2, 3, 4, 5, 6, 7, 8, 9));

            Assert.Equal((1, "one"), mock.Pair());
            Assert.Equal("Lovelace", mock.Nested([(0, 0), (1, 1)]).Person.Last);
            Assert.Equal(2, mock.Nested([(0, 0), (1, 1)]).Item1);
            Assert.Equal(5, mock.Single().Item1);
            Assert.Equal(9, mock.Long().I);
            Assert.Empty(mock.Map() ?? []);

            mock.Received(2).Nested(Arg.Any<(int X, int Y)[]>());
            Assert.True(mock.Calls().Pair() == 1);
        });

    [VerifyTest]
    public void A_generic_interface_closed_over_a_reference_a_value_and_a_nullable_type_is_mocked(
        [Mock] IGeneric<string> reference,
        [Mock] IGeneric<int> value,
        [Mock] IGeneric<string?> nullable)
    {
        Paths.Each(Mock.Of<IGeneric<string>>(), reference, mock =>
        {
            mock.Maybe("a").Returns("b");

            Assert.Equal("b", mock.Maybe("a"));
            mock.Received(1).Maybe("a");
        });
        Paths.Each(Mock.Of<IGeneric<int>>(), value, mock =>
        {
            mock.Maybe(1).Returns(2);
            mock.All().Returns([3]);

            Assert.Equal(2, mock.Maybe(1));
            Assert.Equal([3], mock.All());
            mock.Received(1).Maybe(1);
        });
        Paths.Each(Mock.Of<IGeneric<string?>>(), nullable, mock =>
        {
            mock.Maybe(null).Returns("from null");

            Assert.Equal("from null", mock.Maybe(null));
            mock.Received(1).Maybe(null);
        });
    }
}

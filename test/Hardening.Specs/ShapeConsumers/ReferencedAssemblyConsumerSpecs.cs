extern alias V1;
extern alias V2;

using Shapes.ExternAlias;
using VerificationGlue.Mocks;

namespace Hardening.Specs.ShapeConsumers;

/// <summary>
/// ExternAlias.cs, ExternAliasNested.cs and InternalsVisibleTo.cs: interfaces declared in referenced
/// assemblies, two of which declare the same names and are referenced through extern aliases, and
/// one of which grants this assembly InternalsVisibleTo.
/// </summary>
[Trait("Step", "3")]
public class ReferencedAssemblyConsumerSpecs
{
    [VerifyTest]
    public void Interfaces_with_one_name_in_two_aliased_assemblies_are_mocked_apart([Mock] V1::Vendor.IGateway first, [Mock] V2::Vendor.IGateway second)
    {
        Paths.Each(Mock.Of<V1::Vendor.IGateway>(), first, mock =>
        {
            var payment = new V1::Vendor.Payment();
            var charged = new V1::Vendor.Payment();
            mock.Charge(payment).Returns(charged);

            Assert.Same(charged, mock.Charge(payment));
            mock.Received(1).Charge(payment);
        });
        Paths.Each(Mock.Of<V2::Vendor.IGateway>(), second, mock =>
        {
            var charged = new V2::Vendor.Payment();
            mock.Charge(Arg.Any<V2::Vendor.Payment>(), 3).Returns(charged);

            Assert.Same(charged, mock.Charge(new V2::Vendor.Payment(), 3));
            Assert.Null(mock.Charge(new V2::Vendor.Payment(), 4));
            Assert.True(mock.Calls().Charge(Arg<V2::Vendor.Payment>.Any(), Arg<int>.Any()) == 2);
        });
    }

    [VerifyTest]
    public void Generic_interfaces_closed_over_aliased_and_local_types_are_mocked(
        [Mock] V1::Vendor.IGateway<V2::Vendor.Payment> mixed,
        [Mock] V1::Vendor.IGateway<Local> withLocal,
        [Mock] IList<V2::Vendor.Payment> bclOfAliased)
    {
        Paths.Each(Mock.Of<V1::Vendor.IGateway<V2::Vendor.Payment>>(), mixed, mock =>
        {
            var payment = new V2::Vendor.Payment();
            mock.Charge(payment, Arg.Any<V1::Vendor.Payment>()).Returns(payment);

            Assert.Same(payment, mock.Charge(payment, new V1::Vendor.Payment()));
            mock.Received(1).Charge(payment, Arg.Any<V1::Vendor.Payment>());
        });
        Paths.Each(Mock.Of<V1::Vendor.IGateway<Local>>(), withLocal, mock =>
        {
            var local = new Local();
            mock.Charge(Arg.Any<Local>(), Arg.Any<V1::Vendor.Payment>()).Returns(local);

            Assert.Same(local, mock.Charge(new Local(), new V1::Vendor.Payment()));
            Assert.True(mock.Calls().Charge(Arg<Local>.Any(), Arg<V1::Vendor.Payment>.Any()) == 1);
        });
        Paths.Each(Mock.Of<IList<V2::Vendor.Payment>>(), bclOfAliased, mock =>
        {
            var payment = new V2::Vendor.Payment();
            mock[0].Returns(payment);
            mock.Count.Returns(1);

            mock.Add(payment);

            Assert.Same(payment, mock[0]);
            Assert.Equal(1, mock.Count);
            mock.Received(1).Add(payment);
        });
    }

    [VerifyTest]
    public void A_type_nested_in_an_aliased_generic_type_over_a_local_type_is_mocked([Mock] V1::Vendor.Ledger<Shapes.ExternAliasNested.Local>.IEntry entry) =>
        Paths.Each(
            Mock.Of<V1::Vendor.Ledger<Shapes.ExternAliasNested.Local>.IEntry>(),
            entry,
            mock =>
        {
            var local = new Shapes.ExternAliasNested.Local();
            mock.Value().Returns(local);

            Assert.Same(local, mock.Value());
            mock.Received(1).Value();
        });

    [VerifyTest]
    public void Internal_interfaces_and_members_of_a_friend_assembly_are_mocked([Mock] Friend.IPartlyInternal partly, [Mock] Friend.IInternalDefault withDefault)
    {
        InternalUses.FriendOnly();
        Paths.Each(Mock.Of<Friend.IPartlyInternal>(), partly, mock =>
        {
            mock.Visible().Returns(1);
            mock.Hidden().Returns(2);

            Assert.Equal(1, mock.Visible());
            Assert.Equal(2, mock.Hidden());
            mock.Received(1).Hidden();
        });
        Paths.Each(Mock.Of<Friend.IInternalDefault>(), withDefault, mock =>
        {
            mock.Visible().Returns(3);
            mock.Hidden().Returns(4);

            Assert.Equal(3, mock.Visible());
            Assert.Equal(4, mock.Hidden());
            Assert.True(mock.Calls().Hidden() == 1);
        });
    }

    // A public test method cannot take the internal Friend.IFriendOnly, so it has no [Mock] parameter.
    private static class InternalUses
    {
        public static void FriendOnly() =>
            Paths.Each(Mock.Of<Friend.IFriendOnly>(), mock =>
            {
                mock.Count().Returns(5);

                Assert.Equal(5, mock.Count());
                mock.Received(1).Count();
            });
    }
}

using Shapes.Obsolete;
using VerificationGlue.Mocks;

namespace Hardening.Specs.ShapeConsumers;

/// <summary>Obsolete.cs: obsolete interfaces, an interface that names an obsolete class, and an obsolete member.</summary>
[Trait("Step", "3")]
public class ObsoleteConsumerSpecs
{
    // IOld's [Obsolete] is an error, so only obsolete code can name it, and a test method's signature
    // cannot. Its mock has no [Mock] parameter, and the generic helper finds it through Mock.Of<IOld>().
    [VerifyTest]
    public void An_interface_marked_obsolete_as_an_error_is_mocked_from_obsolete_code()
    {
#pragma warning disable CS0618
        ObsoleteUses.Old();
#pragma warning restore CS0618
    }

#pragma warning disable CS0618
    [VerifyTest]
    public void An_interface_that_names_an_obsolete_class_is_mocked([Mock] IUsesRetired parameter) =>
        ObsoleteUses.UsesRetired(parameter);
#pragma warning restore CS0618

    [VerifyTest]
    public void The_other_members_of_an_interface_with_an_obsolete_member_work([Mock] IObsoleteMember parameter) =>
        Paths.Each(Mock.Of<IObsoleteMember>(), parameter, mock =>
        {
            var runs = 0;
            mock.When(m => m.Run()).Do(_ => runs++);

            mock.Run();

            Assert.Equal(1, runs);
            mock.Received(1).Run();
            Assert.True(mock.Calls().Run() == 1);
        });

    [Obsolete("Names the obsolete shapes.")]
    private static class ObsoleteUses
    {
        public static void Old() =>
            Paths.Each(Mock.Of<IOld>(), mock =>
            {
                mock.Value().Returns(3);

                Assert.Equal(3, mock.Value());
                mock.Received(1).Value();
            });

        public static void UsesRetired(IUsesRetired parameter) =>
            Paths.Each(Mock.Of<IUsesRetired>(), parameter, mock =>
            {
                var retired = new Retired();
                mock.Get().Returns(retired);

                Assert.Same(retired, mock.Get());
                mock.Received(1).Get();
            });
    }
}

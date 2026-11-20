using Shapes.Constraints;
using VerificationGlue.Mocks;

namespace Hardening.Specs.ShapeConsumers;

/// <summary>Constraints.cs: class and method type parameters with each kind of constraint.</summary>
[Trait("Step", "3")]
public class ConstraintConsumerSpecs
{
    [VerifyTest]
    public void A_closed_interface_with_each_class_constraint_is_mocked([Mock] IConstrained<string, int, Guid, int, object> parameter) =>
        Paths.Each(Mock.Of<IConstrained<string, int, Guid, int, object>>(), parameter, mock =>
        {
            var id = Guid.NewGuid();
            var passed = new object();
            mock.Class("a").Returns("b");
            mock.Struct(null).Returns(2);
            mock.NotNull(Arg.Any<Guid>()).Returns(id);
            mock.Create().Returns(7);
            mock.Passthrough().Returns(passed);

            Assert.Equal("b", mock.Class("a"));
            Assert.Equal("", mock.Class("other"));
            Assert.Equal(2, mock.Struct(null));
            Assert.Equal(id, mock.NotNull(Guid.Empty));
            Assert.Equal(7, mock.Create());
            Assert.Same(passed, mock.Passthrough());

            mock.Received(1).Class("a");
            mock.Received(1).Struct(null);
            Assert.True(mock.Calls().NotNull(Arg<Guid>.Any()) == 1);
        });

    // Span<T> allows ref struct type arguments, so its mock cannot box them. It reports VGM0003 and throws.
#pragma warning disable VGM0003
    [VerifyTest]
    public void Generic_methods_with_each_method_constraint_are_mocked([Mock] IGenericMethods parameter) =>
        Paths.Each(Mock.Of<IGenericMethods>(), parameter, mock =>
#pragma warning restore VGM0003
        {
            var error = new InvalidOperationException();
            mock.Find("a").Returns("b");
            mock.Find(3).Returns(4);
            mock.FindClass<string>(null).Returns("found");
            mock.FindStruct<int>(5).Returns(6);
            mock.FindBase<InvalidOperationException>(null).Returns(error);
            mock.Related<Exception, InvalidOperationException>(Arg.Any<Exception>()).Returns(error);
            mock.Many(Arg.Any<IEnumerable<int>>()).Returns([1, 2]);
            var unmanaged = 0;
            mock.When(m => m.Unmanaged(Arg.Any<long>())).Do(info => unmanaged += (int)info.ArgAt<long>(0));

            Assert.Equal("b", mock.Find("a"));
            Assert.Equal(4, mock.Find(3));
            Assert.Equal(0, mock.Find(9));
            Assert.Equal("found", mock.FindClass<string>(null));
            Assert.Equal(6, mock.FindStruct<int>(5));
            Assert.Same(error, mock.FindBase<InvalidOperationException>(null));
            Assert.Same(error, mock.Related<Exception, InvalidOperationException>(new Exception()));
            Assert.Equal([1, 2], mock.Many([3]));
            mock.Unmanaged(4L);
            mock.Unmanaged(4);
            mock.NotNull("x");
            mock.Enum(DayOfWeek.Friday);
            mock.Delegate<Action>(() => { });
            Assert.Throws<NotSupportedException>(() => mock.Span(1));

            Assert.Equal(4, unmanaged);
            mock.Received(1).Find("a");
            mock.Received(1).Find(3);
            mock.Received(1).Unmanaged(4L);
            mock.Received(1).Unmanaged(4);
            mock.Received(1).Enum(DayOfWeek.Friday);
            mock.DidNotReceive().Enum(DayOfWeek.Monday);
            Assert.True(mock.Calls().Find<int>(Arg<int>.Any()) == 2);
        });

    [VerifyTest]
    public void A_generic_interface_closed_over_a_constrained_type_is_mocked([Mock] IOpen<MemoryStream> parameter) =>
        Paths.Each(Mock.Of<IOpen<MemoryStream>>(), parameter, mock =>
        {
            using var stream = new MemoryStream();
            mock.Get().Returns(stream);

            Assert.Same(stream, mock.Get());

            mock.Received(1).Get();
        });

    [VerifyTest]
    public void A_type_parameter_that_allows_ref_struct_works_closed_over_int([Mock] IOpenAllowsRefStruct<int> parameter) =>
        Paths.Each(Mock.Of<IOpenAllowsRefStruct<int>>(), parameter, mock =>
        {
            mock.Get().Returns(3);

            mock.Set(mock.Get());

            mock.Received(1).Set(3);
            Assert.True(mock.Calls().Get() == 1);
        });
}

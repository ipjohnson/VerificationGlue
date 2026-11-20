using Shapes.Parameters;
using VerificationGlue.Mocks;

namespace Hardening.Specs.ShapeConsumers;

/// <summary>Parameters.cs: in, ref readonly, ref, out, params, optional, caller info and scoped parameters.</summary>
[Trait("Step", "3")]
public class ParameterConsumerSpecs
{
    // ParamsSpan takes a ReadOnlySpan<int>, so it reports VGM0003 and throws. SHAPE-007 checks it.
#pragma warning disable VGM0003
    [VerifyTest]
    public void Each_supported_parameter_modifier_passes_its_value_and_writes_back_out_and_ref([Mock] IParameters parameter) =>
        Paths.Each(Mock.Of<IParameters>(), parameter, mock =>
#pragma warning restore VGM0003
        {
            var at = new DateTime(2026, 11, 20);
            var id = Guid.NewGuid();
            mock.Out("seven", out _).Returns(info =>
            {
                info[1] = 7;
                return true;
            });
            mock.When(m => m.Ref(ref Arg.Any<int>())).Do(info => info[0] = info.ArgAt<int>(0) * 2);
            mock.Setup().OutGeneric<string>(Arg<string>.Any(), Arg<string>.Any()).Does(info =>
            {
                info[0] = "out";
                info[1] = "ref " + info.ArgAt<string>(1);
            });
            mock.Params(1, 2, 3).Returns(6);
            mock.ParamsCollection(Arg.Any<IEnumerable<int>>()).Returns(info => info.ArgAt<IEnumerable<int>>(0).Sum());
            mock[1, "a", "b"].Returns(2);

            Assert.True(mock.Out("seven", out var seven));
            Assert.Equal(7, seven);
            Assert.False(mock.Out("other", out var other));
            Assert.Equal(0, other);
            var doubled = 4;
            mock.Ref(ref doubled);
            Assert.Equal(8, doubled);
            var text = "in";
            mock.OutGeneric(out string written, ref text);
            Assert.Equal("out", written);
            Assert.Equal("ref in", text);
            Assert.Equal(6, mock.Params(1, 2, 3));
            Assert.Equal(0, mock.Params());
            Assert.Equal(5, mock.ParamsCollection(2, 3));
            Assert.Equal(2, mock[1, "a", "b"]);
            mock.In(at);
            mock.RefReadonly(in id);
            mock.Optional();
            mock.Optional(1, "name", Mode.Basic, 2m, 0.5);
            mock.Caller();
            var scoped = 1;
            mock.ScopedRef(ref scoped);
            mock.ScopedIn(2L);
            mock[3] = 4;
            Assert.Throws<NotSupportedException>(() => mock.ParamsSpan(1, 2));

            mock.Received(1).In(at);
            mock.Received(1).RefReadonly(in id);
            mock.Received(1).Optional(3, null, Mode.Scientific, 1.5m, double.NaN);
            mock.Received(1).Optional(1, "name", Mode.Basic, 2m, 0.5);
            mock.Received(1).Caller(nameof(Each_supported_parameter_modifier_passes_its_value_and_writes_back_out_and_ref), Arg.Any<int>());
            mock.Received(1).ScopedRef(ref Arg.Any<int>());
            mock.Received(1).ScopedIn(2L);
            mock.Received(1)[3] = 4;
            Assert.True(mock.Calls().Params(Arg<int[]>.Any()) == 2);
            Assert.True(mock.Calls().Ref(4) == 1);
            Assert.True(mock.Calls().Ref(8) == 0);
        });
}

using VerificationGlue.Mocks;
using Xunit;
using static VerificationGlue.VerifySyntax;

#region mocks-generate-mock
[assembly: GenerateMock(typeof(MocksGuide.IClock))]
#endregion

namespace MocksGuide;

#region mocks-generic-helper
public static class Fakes
{
    public static T Fresh<T>()
        where T : class => Mock.Of<T>();
}
#endregion

public class GenericHelper
{
    [Fact]
    public void A_helper_creates_a_mock_named_in_GenerateMock()
    {
        #region mocks-generic-helper-use
        var clock = Fakes.Fresh<IClock>();
        clock.Now.Returns(new DateTime(2026, 11, 17));

        Verify(clock.Now.Year == 2026);
        #endregion
    }
}

using static VerificationGlue.VerifySyntax;

namespace VerificationGlue.Tests.Specs;

// A regression case for each defect that the user accepted as open under the completion criteria in
// docs/design/hardening.md. An open defect's case is skipped and names its issue. A fixed defect's case
// loses its Skip and stays as a regression check. They are here because this project runs on xunit v3,
// which can skip a case, and VerificationGlue.Tests cannot.
public class OpenDefectSpecs
{
    // https://github.com/ipjohnson/VerificationGlue/issues/112, fixed.
    [Fact]
    public void A_plain_string_message_keeps_the_condition_text_and_shows_the_message()
    {
        var width = 4;

        var failure = Assert.Throws<VerifyException>(() => Verify(width == 5, "width is wrong"));

        Assert.StartsWith("Verify failed: width == 5", failure.Message, StringComparison.Ordinal);
        Assert.Contains("width is wrong", failure.Message, StringComparison.Ordinal);
    }
}

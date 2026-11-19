namespace VerificationGlue.Specs;

public class CapturedVariableSpecs
{
    private readonly int _width = 4;

    [VerifyTest]
    public void It_reads_a_captured_local()
    {
        var count = 3;
        Func<bool> lambda = () => count == 1;

        Assert.Equal(3, new CapturedVariable("count").Read<int>(lambda.Target));
    }

    [VerifyTest]
    public void It_reads_a_local_of_an_outer_scope_through_a_loop_scope()
    {
        var most = 5;
        Func<bool>? lambda = null;
        for (var i = 0; i < 1; i++)
        {
            var doubled = i * 2;
            lambda = () => doubled < most;
        }

        Assert.Equal(5, new CapturedVariable("most").Read<int>(lambda!.Target));
        Assert.Equal(0, new CapturedVariable("doubled").Read<int>(lambda.Target));
    }

    [VerifyTest]
    public void It_reads_this_from_a_closure_and_from_an_instance_target()
    {
        var extra = 1;
        Func<bool> withLocal = () => _width + extra == 5;
        Func<bool> onlyThis = () => _width == 5;

        Assert.Same(this, CapturedVariable.This().Read<CapturedVariableSpecs>(withLocal.Target));
        Assert.Same(this, CapturedVariable.This().Read<CapturedVariableSpecs>(onlyThis.Target));
    }

    [VerifyTest]
    public void It_reads_closures_of_another_type_after_the_first()
    {
        var reader = new CapturedVariable("value");

        Assert.Equal(1, reader.Read<int>(Make(1).Target));
        Assert.Equal("two", reader.Read<string>(MakeText("two").Target));
    }

    [VerifyTest]
    public void A_missing_field_throws()
    {
        var count = 3;
        Func<bool> lambda = () => count == 1;

        Assert.Throws<MissingFieldException>(() => new CapturedVariable("other").Read<int>(lambda.Target));
        Assert.Throws<MissingFieldException>(() => new CapturedVariable("count").Read<int>(null));
    }

    private static Func<bool> Make(int value) => () => value == 0;

    private static Func<bool> MakeText(string value) => () => value.Length == 0;
}

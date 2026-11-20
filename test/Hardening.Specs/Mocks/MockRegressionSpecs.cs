namespace Hardening.Specs.Mocks;

/// <summary>
/// The deterministic mock sequences. Each row is the JSON of a sequence, and runs on the reference
/// model and on the mocks as a generated sequence does. A shrunk mismatch from a generated run prints
/// the row to add here.
/// </summary>
[Trait("Step", "3")]
public class MockRegressionSpecs
{
    [VerifyTest]
    // A Does callback wrote the by-value key of a recursive member, and the next call with that key got a new mock. Seed 1, case 9456.
    [Row("""[{"op":"fetch","Slot":0,"Site":{"Member":"Child","Target":0,"Args":[{"Kind":"Exact","Value":"2","Pred":"Even"}]}},{"op":"typed","Site":{"Member":"Child","Target":0,"Args":[{"Kind":"Exact","Value":"2","Pred":"Even"}]},"Does":{"Id":2,"Writes":true}},{"op":"invoke","Site":{"Member":"Child","Target":0,"Args":[{"Kind":"Exact","Value":"2","Pred":"Even"}]}}]""")]
    // Calls in parallel flows take the values of one response sequence in the order they run, and a callback runs on the call it matches. Seed 2, case 6597.
    [Row("""[{"op":"when","Site":{"Member":"Name","Target":0,"Args":[{"Kind":"Exact","Value":"","Pred":"Even"}]},"Callback":{"Id":1,"Writes":false},"Throw":false},{"op":"setup","Site":{"Member":"Name","Target":0,"Args":[{"Kind":"Pred","Pred":"NullOrEmpty"}]},"Response":{"Kind":"Sequence","Values":["x","y","x"],"Id":0},"Await":false},{"op":"parallel","Branches":[[{"op":"invoke","Site":{"Member":"Name","Target":0,"Args":[{"Kind":"Exact","Pred":"Even"}]}},{"op":"invoke","Site":{"Member":"Name","Target":0,"Args":[{"Kind":"Exact","Value":"","Pred":"Even"}]}}],[{"op":"invoke","Site":{"Member":"Name","Target":0,"Args":[{"Kind":"Exact","Pred":"Even"}]}}]]}]""")]
    public async Task A_regression_sequence_matches_the_reference_model(string json)
    {
        var result = await MockCampaign.RunAsync(Sequence.FromJson("Regression", json));

        Assert.True(result.Passed, $"{result.MismatchKind} mismatch: {result.Detail}\n{result.Sequence}\nmodel: {string.Join(" / ", result.Expected)}\nmocks: {string.Join(" / ", result.Actual)}");
    }
}

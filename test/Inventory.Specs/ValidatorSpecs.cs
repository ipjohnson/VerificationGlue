using System.Text.Json;

namespace Inventory.Specs;

public class ValidatorSpecs
{
    private const string ThisFile = "test/Inventory.Specs/ValidatorSpecs.cs";
    private const string AnExistingTest = ThisFile + "#ValidatorSpecs.A_valid_inventory_has_no_problems";

    private static readonly Scenario Supported = new()
    {
        Id = "VERIFY-001",
        Contract = "Verify throws when the condition is false.",
        Source = "docs/guide/getting-started-verify.md#write-checks",
        Support = "supported",
        Evidence = [AnExistingTest],
    };

    private static readonly ContractInventory Valid = new()
    {
        Baseline = "0123456789abcdef0123456789abcdef01234567",
        AllowGaps = true,
        Areas = [new Area { Prefix = "VERIFY", Name = "Verify" }],
        Decisions = [],
        Scenarios = [Supported],
    };

    private readonly InventoryValidator validator = new(RepositoryRoot.Path);

    [VerifyTest]
    public void A_valid_inventory_has_no_problems()
    {
        Assert.Empty(validator.Validate(Valid));
    }

    [VerifyTest]
    public void A_Scenario_ID_with_an_unknown_area_is_rejected()
    {
        var problems = validator.Validate(Valid with { Scenarios = [Supported with { Id = "ASSERT-001" }] });

        Assert.Equal(["ASSERT-001: the Scenario ID names the unknown area ASSERT"], problems);
    }

    [VerifyTest]
    public void A_Scenario_ID_that_is_not_of_the_form_AREA_000_is_rejected()
    {
        var problems = validator.Validate(Valid with { Scenarios = [Supported with { Id = "VERIFY-1" }] });

        Assert.Equal(["VERIFY-1: the Scenario ID is not of the form AREA-000"], problems);
    }

    [VerifyTest]
    public void A_Scenario_ID_used_twice_is_rejected()
    {
        var problems = validator.Validate(Valid with { Scenarios = [Supported, Supported] });

        Assert.Equal(["VERIFY-001: the Scenario ID is used more than once"], problems);
    }

    [VerifyTest]
    public void A_decision_that_names_an_unknown_Scenario_ID_is_rejected()
    {
        var decision = new Decision { Id = "DEC-01", Question = "q", ProposedOwner = "the user", Approved = false, Scenarios = ["VERIFY-002"] };

        var problems = validator.Validate(Valid with { Decisions = [decision] });

        Assert.Equal(["DEC-01: the decision names the unknown Scenario ID VERIFY-002"], problems);
    }

    [VerifyTest]
    [Row("test/Inventory.Specs/Missing.cs#ValidatorSpecs.Any", "the file test/Inventory.Specs/Missing.cs does not exist")]
    [Row(ThisFile + "#MissingSpecs.Any", ThisFile + " declares no type MissingSpecs")]
    [Row(ThisFile + "#ValidatorSpecs.No_such_test", "ValidatorSpecs in " + ThisFile + " has no method No_such_test")]
    [Row(ThisFile + "#ValidatorSpecs.NotATest", "ValidatorSpecs.NotATest in " + ThisFile + " has no test attribute")]
    [Row(ThisFile + "#NoDot", "'NoDot' is not of the form Type.Method")]
    [Row("samples/NoSuchSample", "samples/NoSuchSample does not exist")]
    [Row("docs", "the directory docs holds no project")]
    [Row("../outside.cs#A.B", "'../outside.cs' is not a path relative to the repository root")]
    public void A_test_reference_that_does_not_exist_is_rejected(string reference, string problem)
    {
        var problems = validator.Validate(Valid with { Scenarios = [Supported with { Evidence = [reference] }] });

        Assert.Equal([$"VERIFY-001: evidence '{reference}': {problem}"], problems);
    }

    [VerifyTest]
    [Row(ThisFile + "#ValidatorSpecs.Nested.A_nested_test")]
    [Row("samples/VerifyInXunit")]
    [Row("eng/pack-and-consume.sh")]
    public void References_to_nested_tests_projects_and_files_are_accepted(string reference)
    {
        Assert.Empty(validator.Validate(Valid with { Scenarios = [Supported with { Evidence = [reference] }] }));
    }

    [VerifyTest]
    public void An_entry_marked_as_an_explicit_gap_is_accepted()
    {
        var gap = Supported with { Evidence = [], Gap = new Gap { Size = "m", Step = 2, Missing = "No test checks it." } };

        Assert.Empty(validator.Validate(Valid with { Scenarios = [gap] }));
    }

    [VerifyTest]
    public void An_entry_with_no_evidence_and_no_gap_is_rejected()
    {
        var problems = validator.Validate(Valid with { Scenarios = [Supported with { Evidence = [] }] });

        Assert.Equal(["VERIFY-001: the entry has no evidence and is not marked as an explicit gap"], problems);
    }

    [VerifyTest]
    public void A_gap_needs_a_known_size_and_a_step_from_2_to_5()
    {
        var gap = Supported with { Gap = new Gap { Size = "huge", Step = 1, Missing = "x" } };

        var problems = validator.Validate(Valid with { Scenarios = [gap] });

        Assert.Equal(
            [
                "VERIFY-001: the gap size 'huge' is not one of xs, s, m, l, xl",
                "VERIFY-001: the gap names step 1, and only steps 2 to 5 fill gaps",
            ],
            problems);
    }

    [VerifyTest]
    public void A_supported_gap_fails_once_gaps_are_no_longer_allowed()
    {
        var gap = Supported with { Evidence = [], Gap = new Gap { Size = "s", Step = 4, Missing = "x" } };

        var problems = validator.Validate(Valid with { AllowGaps = false, Scenarios = [gap] });

        Assert.Equal(["VERIFY-001: a supported entry has a gap, and the inventory no longer allows gaps"], problems);
    }

    [VerifyTest]
    public void An_unsupported_entry_needs_its_expected_outcome()
    {
        var problems = validator.Validate(Valid with { Scenarios = [Supported with { Support = "unsupported" }] });

        Assert.Equal(["VERIFY-001: an unsupported entry needs its expected refusal, diagnostic or fallback in outcome"], problems);
    }

    [VerifyTest]
    public void An_unsupported_entry_with_an_outcome_is_accepted()
    {
        var unsupported = Supported with
        {
            Support = "unsupported",
            Outcome = new Outcome { Kind = "refusal", Detail = "VGM0001 names the static abstract member." },
        };

        Assert.Empty(validator.Validate(Valid with { Scenarios = [unsupported] }));
    }

    [VerifyTest]
    public void A_source_anchor_with_no_heading_is_rejected()
    {
        var problems = validator.Validate(Valid with { Scenarios = [Supported with { Source = "docs/guide/check.md#no-such-heading" }] });

        Assert.Equal(["VERIFY-001: source: docs/guide/check.md has no heading with the anchor #no-such-heading"], problems);
    }

    [VerifyTest]
    public void A_verification_status_in_an_entry_fails_to_load()
    {
        const string json = """
            {
              "baseline": "0123456789abcdef0123456789abcdef01234567",
              "allowGaps": true,
              "areas": [],
              "decisions": [],
              "scenarios": [
                { "id": "VERIFY-001", "contract": "c", "source": "docs/guide/check.md", "support": "supported", "evidence": [], "verification": "passed" }
              ]
            }
            """;

        var failure = Assert.Throws<JsonException>(() => ContractInventory.Parse(json));

        Assert.Contains("verification", failure.Message, StringComparison.Ordinal);
    }

    [VerifyTest]
    public void The_gap_list_must_match_the_inventory_gaps()
    {
        var gap = Supported with { Evidence = [], Gap = new Gap { Size = "m", Step = 2, Missing = "x" } };
        const string markdown = """
            ### Gap list

            | Scenario ID | Contract | Size | Step |
            |---|---|---|---|
            | `VERIFY-001` | Verify | s | 2 |
            | `VERIFY-009` | Unknown | m | 3 |

            ### Contract decisions

            | ID | Decision | Proposed owner | Scenarios |
            |---|---|---|---|
            """;

        var problems = new InventoryValidator(RepositoryRoot.Path).ValidateDesignDoc(Valid with { Scenarios = [gap] }, markdown);

        Assert.Equal(
            [
                "the gap list is missing or misstates the gap 'VERIFY-001 m 2' (ID, size, step)",
                "the gap list has the row 'VERIFY-001 s 2' (ID, size, step), which no inventory gap matches",
                "the gap list has the row 'VERIFY-009 m 3' (ID, size, step), which no inventory gap matches",
            ],
            problems);
    }

    internal static void NotATest()
    {
    }

    public class Nested
    {
        [VerifyTest]
        public void A_nested_test()
        {
        }
    }
}

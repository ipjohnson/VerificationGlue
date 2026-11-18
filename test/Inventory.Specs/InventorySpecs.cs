namespace Inventory.Specs;

public class InventorySpecs
{
    private const string DesignDoc = "docs/design/hardening.md";

    [VerifyTest]
    public void The_checked_in_inventory_is_valid()
    {
        var inventory = ContractInventory.Load(RepositoryRoot.Path);

        AssertNoProblems(new InventoryValidator(RepositoryRoot.Path).Validate(inventory));
    }

    [VerifyTest]
    public void The_gap_list_and_decisions_in_the_design_doc_match_the_inventory()
    {
        var inventory = ContractInventory.Load(RepositoryRoot.Path);
        var markdown = File.ReadAllText(Path.Combine(RepositoryRoot.Path, DesignDoc));

        AssertNoProblems(new InventoryValidator(RepositoryRoot.Path).ValidateDesignDoc(inventory, markdown));
    }

    [VerifyTest]
    public void The_inventory_covers_each_area_that_step_1_names()
    {
        var inventory = ContractInventory.Load(RepositoryRoot.Path);
        string[] required = ["VERIFY", "CHECK", "SCOPE", "THROWS", "MOCK", "PARAM", "LIFE"];

        var empty = required.Where(prefix => !inventory.Scenarios.Any(scenario => scenario.Id.StartsWith(prefix + "-", StringComparison.Ordinal)));

        Assert.Empty(empty);
    }

    private static void AssertNoProblems(IReadOnlyList<string> problems) =>
        Assert.True(problems.Count == 0, $"{problems.Count} problems:\n" + string.Join("\n", problems));
}

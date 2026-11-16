namespace Generator.Harness.Specs;

public class SnapshotSpecs
{
    [Fact]
    public void Mismatch_writes_the_received_file_and_names_the_first_different_line()
    {
        Assert.SkipWhen(Environment.GetEnvironmentVariable(Snapshot.AcceptVariable) == "1", "Accepting snapshots would overwrite this spec's verified file.");
        var received = Path.Combine(ProjectDirectory(), "Snapshots", $"{nameof(SnapshotSpecs)}.{nameof(Mismatch_writes_the_received_file_and_names_the_first_different_line)}.received.txt");
        try
        {
            var error = Assert.Throws<SnapshotMismatchException>(() =>
                Snapshot.Match(typeof(SnapshotSpecs), nameof(Mismatch_writes_the_received_file_and_names_the_first_different_line), "first\nchanged\n"));

            Assert.Contains("Line 2: expected `second`, received `changed`.", error.Message, StringComparison.Ordinal);
            Assert.Equal("first\nchanged\n", File.ReadAllText(received));
        }
        finally
        {
            File.Delete(received);
        }
    }

    [Fact]
    public void Match_ignores_carriage_returns()
    {
        Snapshot.Match(typeof(SnapshotSpecs), nameof(Match_ignores_carriage_returns), "first\r\nsecond\r\n");
    }

    private static string ProjectDirectory() =>
        typeof(SnapshotSpecs).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == Snapshot.ProjectDirectoryKey).Value!;
}

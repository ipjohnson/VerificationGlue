# Writing docs

The docs are built from the product. A code block comes from a sample that builds, each diagnostic has a page, and CI checks links and style. This page shows how to work with each check.

## Code blocks come from samples

Mark the code in a file under `samples/` with a named region. In C# use `#region` with the snippet name:

```cs
#region mocks-returns-basic
var calculator = Mock.Of<ICalculator>();
#endregion
```

In XML files, such as a `.csproj`, use comments: `begin-snippet: name` before the code and `end-snippet` after it, each inside an HTML comment.

In the markdown page, put the reference on its own line as an HTML comment, `snippet: name`, followed by an `endSnippet` comment. Then run the tool from the repository root:

```sh
dotnet tool restore
dotnet mdsnippets
```

[MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets) replaces the text between the two comments with the current code and a link to its source. Commit the result. CI runs the tool again and fails when the snippet name does not resolve or when the committed page differs from the expanded one. [`docs/diagnostics/VGT0007.md`](diagnostics/VGT0007.md) shows an expanded snippet.

The sample must build in CI. Add a new sample project to `VerificationGlue.slnx`, or to `eng/pack-and-consume.sh` when it must consume the packed packages.

## Each diagnostic has a page

Every `DiagnosticDescriptor` in the five generator assemblies has a page at `docs/diagnostics/<ID>.md`. Its `HelpLinkUri` points to that page on `main`. The page starts with `# <ID>: <title>` and has these sections:

- What it means
- Example that triggers it
- The fix
- When to suppress it

An MSBuild `Error` task with a `VG` code in a package's `.targets` file, such as VGT1001, counts as a descriptor. Its `HelpLink` points to its page.

`test/Docs.Specs/DiagnosticPageSpecs.cs` fails when a descriptor or build error has no page, a page has neither, a help link is wrong, or a section is missing.

## Links and style

CI runs [lychee](https://lychee.cli.rs) on every markdown file outside `spikes/` and fails on a broken link or a missing anchor. It also runs Vale with the rules in [`STYLE.md`](STYLE.md).

`eng/check-docs.sh` runs the snippet, link and style checks as CI does. It needs `lychee` and `vale` on `PATH`. Pass `--offline` to skip links to other sites.

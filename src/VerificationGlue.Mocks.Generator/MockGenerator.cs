using Microsoft.CodeAnalysis;

namespace VerificationGlue.Mocks.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class MockGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}

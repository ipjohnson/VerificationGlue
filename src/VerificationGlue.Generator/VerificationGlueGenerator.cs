using Microsoft.CodeAnalysis;

namespace VerificationGlue.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class VerificationGlueGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}

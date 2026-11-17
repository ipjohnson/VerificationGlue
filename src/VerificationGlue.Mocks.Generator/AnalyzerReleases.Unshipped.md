; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VGM0001 | VerificationGlue.Mocks | Error | MockDiagnostics
VGM0002 | VerificationGlue.Mocks | Warning | MockDiagnostics, Error for an NSubstitute-style Arg passed to Setup() or Calls()
VGM0003 | VerificationGlue.Mocks | Warning | MockDiagnostics
VGM0004 | VerificationGlue.Mocks | Warning | MockDiagnostics
VGM0005 | VerificationGlue.Mocks | Error | MockDiagnostics
VGM0007 | VerificationGlue.Mocks | Info | MockDiagnostics

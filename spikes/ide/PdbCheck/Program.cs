// Prints the sequence points of every method in a portable PDB whose name ends in __Verified,
// so the report can show which original lines a debugger binds to in each copy.
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

var dll = args[0];
using var peReader = new PEReader(File.OpenRead(dll));
var metadata = peReader.GetMetadataReader();
using var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(Path.ChangeExtension(dll, ".pdb")));
var pdb = pdbProvider.GetMetadataReader();

foreach (var handle in metadata.MethodDefinitions)
{
    var method = metadata.GetMethodDefinition(handle);
    var name = metadata.GetString(method.Name);
    if (!name.EndsWith("__Verified", StringComparison.Ordinal))
    {
        continue;
    }

    Console.WriteLine(name);
    var debug = pdb.GetMethodDebugInformation(MetadataTokens.MethodDefinitionHandle(MetadataTokens.GetRowNumber(handle)));
    foreach (var point in debug.GetSequencePoints())
    {
        if (point.IsHidden)
        {
            Console.WriteLine($"  IL_{point.Offset:X4} hidden");
            continue;
        }
        var document = pdb.GetString(pdb.GetDocument(point.Document).Name);
        Console.WriteLine($"  IL_{point.Offset:X4} {Path.GetFileName(document)} {point.StartLine}:{point.StartColumn}-{point.EndLine}:{point.EndColumn}   ({document})");
    }

    foreach (var scopeHandle in pdb.GetLocalScopes(MetadataTokens.MethodDefinitionHandle(MetadataTokens.GetRowNumber(handle))))
    {
        var scope = pdb.GetLocalScope(scopeHandle);
        var locals = scope.GetLocalVariables().Select(l => pdb.GetString(pdb.GetLocalVariable(l).Name)).ToList();
        if (locals.Count > 0)
        {
            Console.WriteLine($"  scope IL_{scope.StartOffset:X4}-IL_{scope.EndOffset:X4}: {string.Join(", ", locals)}");
        }
    }
}

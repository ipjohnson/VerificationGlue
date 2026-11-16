using System.Linq.Expressions;

var x = new P("bob", 12);
P? n = null;
var list = new List<int> { 1, 2 };
ReadOnlySpan<char> span = "abc";

#if BAD
Expression<Func<bool>> e1 = () => x is { Name: "bob", Age: > 18 };
Expression<Func<bool>> e2 = () => x is not null;
Expression<Func<bool>> e3 = () => n?.Age > 3;
Expression<Func<Task<bool>>> e4 = async () => await Task.FromResult(1) == 1;
Expression<Func<bool>> e5 = () => (1, 2) == (1, 2);
Expression<Func<bool>> e6 = () => list is [1, ..];
Expression<Func<bool>> e7 = () => list.Count switch { 2 => true, _ => false };
Expression<Func<bool>> e8 = () => span.Length == 3;
Expression<Func<bool>> e9 = () => int.TryParse("1", out var v) && v == 1;
Expression<Func<bool>> e10 = () => list.SequenceEqual([1, 2]);
#endif

Expression<Func<bool>> ok = () => list.Count > 3;
var sw = System.Diagnostics.Stopwatch.StartNew();
var result = ok.Compile()();
Console.WriteLine($"{ok.Body} => {result}, first Compile+invoke took {sw.Elapsed.TotalMilliseconds:F1} ms, dynamic code supported: {System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported}");
sw.Restart();
for (var i = 0; i < 1000; i++) { Expression<Func<bool>> again = () => list.Count > i; again.Compile()(); }
Console.WriteLine($"1000 x build+Compile+invoke: {sw.Elapsed.TotalMilliseconds:F1} ms");

record P(string Name, int Age);

using VerificationGlue;
using static VerificationGlue.VerifySyntax;

#region verify-under-native-aot
var total = 40;
var failure = Throws<VerifyException>(() => Verify(total == 30, $"total is {total}"));
Console.WriteLine(failure.Message);
#endregion

Verify(failure.Message == """
    Verify failed: total == 30
      expected total to equal 30
      total is 40
      at Program.cs:6
    """);

var wrong = Throws<VerifyException>(() => Throws<InvalidOperationException>(() => throw new FormatException("bad digit")));
Verify(wrong.Message == """
    Throws failed: () => throw new FormatException("bad digit")
      expected throw new FormatException("bad digit") to throw InvalidOperationException
      it threw System.FormatException: bad digit
      at Program.cs:17
    """);

var none = await ThrowsAsync<VerifyException>(() => ThrowsAsync<TimeoutException>(() => Task.CompletedTask));
Verify(none.Message == """
    ThrowsAsync failed: () => Task.CompletedTask
      expected Task.CompletedTask to throw TimeoutException
      no exception was thrown
      at Program.cs:25
    """);

var derived = Throws<VerifyException>(() => ThrowsExactly<ArgumentException>(() => throw new ArgumentNullException("name")));
Verify(derived.Message == """
    ThrowsExactly failed: () => throw new ArgumentNullException("name")
      expected exactly ArgumentException, but ArgumentNullException was thrown
      at Program.cs:33
    """);

var derivedAsync = await ThrowsAsync<VerifyException>(() => ThrowsExactlyAsync<ArgumentException>(() => Task.FromException(new ArgumentNullException("name"))));
Verify(derivedAsync.Message == """
    ThrowsExactlyAsync failed: () => Task.FromException(new ArgumentNullException("name"))
      expected exactly ArgumentException, but ArgumentNullException was thrown
      at Program.cs:40
    """);

List<string> names = ["Ann", "Bo"];
var several = Throws<VerifyException>(() => Single(names));
Verify(several.Message == """
    Single failed: names
      expected one item, but found 2: ["Ann", "Bo"]
      at Program.cs:48
    """);

var noMatch = Throws<VerifyException>(() => Single(names, n => n.Length > 5));
Verify(noMatch.Message == """
    Single failed: names, n => n.Length > 5
      expected one match, but found 0
      at Program.cs:55
    """);

object shape = new List<int>();
var wrongType = Throws<VerifyException>(() => Is<Dictionary<string, int>>(shape));
Verify(wrongType.Message == """
    Is failed: shape
      expected Dictionary<String, Int32>, but it was List<Int32>
      at Program.cs:63
    """);

string? label = null;
var missing = Throws<VerifyException>(() => NotNull(label));
Verify(missing.Message == """
    NotNull failed: label
      at Program.cs:71
    """);

int? count = null;
var noValue = Throws<VerifyException>(() => NotNull(count));
Verify(noValue.Message == """
    NotNull failed: count
      at Program.cs:78
    """);

// Tier 3: the rewritten copy of a [Verify] method adds the tolerance line before the values.
var apart = Throws<VerifyException>(() => ToleranceChecks.VerifyLeft(new Box(120.5625, 40)));
Verify(apart.Message == """
    Verify failed: Math.Abs(box.X - 120.5) <= 0.01
      box.X differs from 120.5 by 0.0625, tolerance 0.01
      Math.Abs(box.X - 120.5) → 0.0625
      box.X - 120.5           → 0.0625
      box.X                   → 120.5625
      box                     → Box { X = 120.5625, Y = 40 }
      at Program.cs:146
    """);

// Tier 3: a failed == on two strings shows where they first differ.
var differ = Throws<VerifyException>(() => StringChecks.VerifyText("a\r\nb\r\nc", "a\nb\nc"));
Verify(differ.Message == """
    Verify failed: written == expected
      written  → "a\r\nb\r\nc"
      expected → "a\nb\nc"
      first difference at line 1, column 2
      - 1   a\r
      + 1   a
      - 2   b\r
      + 2   b
        3   c
      at Program.cs:155
    """);

Verify(Single(names, n => n.Length == 2) == "Bo");
Verify(Is<List<int>>(shape).Count == 0);
Verify(NotNull((int?)3) == 3);

var expected = 60;
var captured = Throws<VerifyException>(() => Verify(() => total + 10 == expected));
Verify(captured.Message == """
    Verify failed: total + 10 == expected
      total + 10 → 50
      total      → 40
      expected   → 60
      at Program.cs:116
    """);

var queue = new Queue<int>([1, 2, 3]);
var once = Throws<VerifyException>(() => Verify(() => queue.Dequeue() == 7));
Verify(queue.Count == 2);
Verify(once.Message.Contains("queue.Dequeue() → 1"));

var members = Throws<VerifyException>(new Counter().Check);
Verify(members.Message == """
    Verify failed: _count == 3
      _count → 2
      at Program.cs:163
    """);

Console.WriteLine("VerifyAot passed.");

public sealed record Box(double X, double Y);

public static partial class ToleranceChecks
{
    [Verify]
    public static void VerifyLeft(Box box)
    {
        Verify(Math.Abs(box.X - 120.5) <= 0.01);
    }
}

public static partial class StringChecks
{
    [Verify]
    public static void VerifyText(string written, string expected)
    {
        Verify(written == expected);
    }
}

sealed class Counter
{
    private readonly int _count = 2;

    public void Check() => Verify(() => _count == 3);
}

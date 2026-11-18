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

Console.WriteLine("VerifyAot passed.");

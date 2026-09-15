using System.Text;
using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// <c>legal/PRIVACY_POLICY.md</c> §3 (P4 follow-up, 2026-09-15): a user name in a path never leaves the machine in a log
/// copy, in any spelling a log carries, and nothing else in the file changes.
/// </summary>
public sealed class LogRedactorTests
{
    private static readonly LogRedactor _redactor = new(@"C:\Users\kuujo");

    [Theory]
    [InlineData(@"ledger C:\Users\kuujo\AppData\Local\FrameLedger\ledger.db", @"ledger C:\Users\<user>\AppData\Local\FrameLedger\ledger.db")]
    [InlineData(@"{""Path"":""C:\\Users\\kuujo\\AppData\\Local""}", @"{""Path"":""C:\\Users\\<user>\\AppData\\Local""}")]
    [InlineData("file:///c:/users/KUUJO/Documents/x.txt", "file:///c:/users/<user>/Documents/x.txt")]
    [InlineData(@"C:\Users\John Smith\Documents", @"C:\Users\<user>\Documents")]
    [InlineData(@"'C:\Users\kuujo'", @"'C:\Users\<user>'")]
    [InlineData("profile C:\\Users\\kuujo\r\nnext", "profile C:\\Users\\<user>\r\nnext")]
    [InlineData(@"image C:\Users\Nguy?n\Games\title.exe", @"image C:\Users\<user>\Games\title.exe")]
    [InlineData(@"two C:\Users\a\x and D:\Users\b\y", @"two C:\Users\<user>\x and D:\Users\<user>\y")]
    public void TheNameInAUsersPathBecomesThePlaceholderInEverySpelling(string log, string expected)
    {
        _redactor.Redact(log).Should().Be(expected);
        _redactor.Redact(_redactor.Redact(log)).Should().Be(expected, "redacting a copy twice changes nothing more");
    }

    [Theory]
    [InlineData(@"C:\Program Files\FrameLedger\FrameLedger.exe")]
    [InlineData(@"D:\Games\Users\title.exe")]
    [InlineData(@"C:\Userspace\x")]
    [InlineData("Users: 3, sessions: 12")]
    [InlineData(@"C:\Users\")]
    public void TextThatNamesNoUserIsUnchanged(string log) => _redactor.Redact(log).Should().Be(log);

    [Fact]
    public void AProfileOutsideTheUsersFolderIsRedactedByItsOwnPath()
    {
        var redirected = new LogRedactor(@"D:\Profiles\kuujo");

        redirected.Redact(@"ledger D:\Profiles\kuujo\AppData\Local\FrameLedger\ledger.db").Should().Be(@"ledger D:\Profiles\<user>\AppData\Local\FrameLedger\ledger.db");
        redirected.Redact(@"{""P"":""d:\\profiles\\KUUJO\\x""}").Should().Be(@"{""P"":""d:\\profiles\\<user>\\x""}");
        redirected.Redact(@"D:\Profiles\kuujo2\x").Should().Be(@"D:\Profiles\kuujo2\x", "another folder that merely starts with the name");
        new LogRedactor(null).Redact(@"D:\Profiles\kuujo\x").Should().Be(@"D:\Profiles\kuujo\x", "without a profile only X:\\Users paths are known");
    }

    [Fact]
    public void BytesOutsideTheNamesAreKeptExactlyInUtf8AndInAnsi()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("[12:00:00.000 INF] Thiết lập đã lưu C:\\Users\\Nguyễn\\AppData\r\n");
        byte[] expectedUtf8 = Encoding.UTF8.GetBytes("[12:00:00.000 INF] Thiết lập đã lưu C:\\Users\\<user>\\AppData\r\n");
        byte[] ansi = [.. Encoding.ASCII.GetBytes("# image C:\\Users\\Jos"), 0xE9, .. Encoding.ASCII.GetBytes("\\Games\\t.exe\n")];
        byte[] expectedAnsi = Encoding.ASCII.GetBytes("# image C:\\Users\\<user>\\Games\\t.exe\n");

        _redactor.Redact(utf8).Should().Equal(expectedUtf8);
        _redactor.Redact(ansi).Should().Equal(expectedAnsi);
    }
}

using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// One App per data folder (2026-09-22): the owner's log showed two started 360 ms apart, each with a shell and a tray
/// icon. The first claim wins; a later one is told so, asks the first to show itself, and must not start; and the
/// claim ends with the process that holds it.
/// </summary>
public sealed class SingleInstanceTests
{
    private static string UniqueKey() => SingleInstance.KeyOf(@"C:\fl-tests\" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TheFirstStartClaimsTheFolderAndALaterOneHandsOverAndIsRefused()
    {
        string key = UniqueKey();
        SingleInstance.TryClaim(key, out SingleInstance? first).Should().Be(SingleInstance.Claim.Acquired);
        using (first)
        {
            first.Should().NotBeNull();
            using var revealed = new ManualResetEventSlim(false);
            first!.RevealRequested += (_, _) => revealed.Set();

            SingleInstance.Claim later = SingleInstance.TryClaim(key, out SingleInstance? second);
            using (second)
            {
                later.Should().Be(SingleInstance.Claim.AnotherInstanceIsRunning);
                second.Should().BeNull("the second start gets nothing to hold: it exits");
            }

            revealed.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).Should().BeTrue("the running instance is asked to bring its window forward");

            // And again: the event is not one-shot, because the user can click the shortcut any number of times.
            revealed.Reset();
            SingleInstance.Claim third = SingleInstance.TryClaim(key, out SingleInstance? none);
            none?.Dispose();
            third.Should().Be(SingleInstance.Claim.AnotherInstanceIsRunning);
            revealed.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).Should().BeTrue();
        }
    }

    [Fact]
    public void TheClaimEndsWithItsHolderAndAnotherFolderIsAnotherInstance()
    {
        string key = UniqueKey();
        SingleInstance.TryClaim(key, out SingleInstance? first).Should().Be(SingleInstance.Claim.Acquired);
        first!.Dispose();
        first.Dispose();

        SingleInstance.TryClaim(key, out SingleInstance? again).Should().Be(SingleInstance.Claim.Acquired, "the App that held it has exited");
        using (again)
        {
            SingleInstance.TryClaim(UniqueKey(), out SingleInstance? elsewhere).Should().Be(SingleInstance.Claim.Acquired, "an App pointed at another data folder is its own instance");
            elsewhere!.Dispose();
        }
    }

    [Fact]
    public void TheKeyIsTheFolderWhateverItsCaseOrTrailingSeparator()
    {
        string key = SingleInstance.KeyOf(@"C:\Users\someone\AppData\Local\FrameLedger");

        SingleInstance.KeyOf(@"c:\users\SOMEONE\appdata\local\frameledger\").Should().Be(key);
        SingleInstance.KeyOf(@"C:\Users\someone\AppData\Local\Other").Should().NotBe(key);
        key.Should().MatchRegex("^[0-9A-F]{16}$", "a path is not a legal kernel object name; a hash is");
    }
}

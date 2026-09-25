using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using FluentAssertions;
using FrameLedger.Infrastructure.AntiCheat;
using FrameLedger.Infrastructure.Capture;
using FrameLedger.Shared;

namespace FrameLedger.Infrastructure.Tests.Capture;

/// <summary>
/// D33 (owner decision 2026-09-26): the Agent's channel to the Overlay — <c>Local\FrameLedger.Tolerate.&lt;pid&gt;</c> — says
/// the family in the layout <c>fl_tolerance.h</c> fixes, is readable by the user and writable by nobody, refuses a name that
/// already exists, and is gone when it is let go.
/// </summary>
public sealed class OverlayToleranceChannelTests
{
    /// <summary>A pid for the name alone: nothing runs as it, and each test has its own.</summary>
    private static int NamePid() => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1_000_000, int.MaxValue);

    private static unsafe string FamilyOf(FlTolerance t) => Encoding.ASCII.GetString(new ReadOnlySpan<byte>(t.Families, ToleranceLayout.NameLen)).TrimEnd('\0');

    [Fact]
    public void ThePublishedMappingNamesTheFamilyAndNobodyMayWriteIt()
    {
        int pid = NamePid();
        using IDisposable? held = new OverlayToleranceChannel().TryPublish(pid, "NetEase Yidun");
        held.Should().NotBeNull();

        using var mapping = MemoryMappedFile.OpenExisting(ToleranceLayout.MappingName(pid), MemoryMappedFileRights.Read);
        using MemoryMappedViewAccessor view = mapping.CreateViewAccessor(0, Marshal.SizeOf<FlTolerance>(), MemoryMappedFileAccess.Read);
        view.Read(0, out FlTolerance t);
        t.Magic.Should().Be(ToleranceLayout.Magic);
        t.Version.Should().Be(ToleranceLayout.Version);
        t.Count.Should().Be(1);
        t.Reserved.Should().Be(0);
        FamilyOf(t).Should().Be("NetEase Yidun");

        Action write = () => MemoryMappedFile.OpenExisting(ToleranceLayout.MappingName(pid), MemoryMappedFileRights.ReadWrite).Dispose();
        write.Should().Throw<UnauthorizedAccessException>("the DACL grants the user read and nothing else: the Agent wrote once, through the creating handle");
    }

    [Fact]
    public void ANameAlreadyTakenIsNotOurs()
    {
        int pid = NamePid();
        using var squatter = MemoryMappedFile.CreateNew(ToleranceLayout.MappingName(pid), 528);

        new OverlayToleranceChannel().TryPublish(pid, "NetEase Yidun").Should().BeNull("whatever that mapping says, nothing is injected under it");
    }

    [Fact]
    public void LettingGoRemovesTheNameAndTwiceIsHarmless()
    {
        int pid = NamePid();
        var channel = new OverlayToleranceChannel();
        IDisposable held = channel.TryPublish(pid, "Anybrain")!;

        held.Dispose();
        held.Dispose();

        Action open = () => MemoryMappedFile.OpenExisting(ToleranceLayout.MappingName(pid), MemoryMappedFileRights.Read).Dispose();
        open.Should().Throw<FileNotFoundException>();
        using IDisposable? again = channel.TryPublish(pid, "Anybrain");
        again.Should().NotBeNull("the next session for a recycled pid can publish again");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Net\nEase")]
    [InlineData("Ñetease")]
    [InlineData("a family name of sixty-four characters is one more than the Overlay reads")]
    public void ANameTheOverlayCouldNeverMatchIsNotPublished(string family)
    {
        OverlayToleranceChannel.Encode(family).Should().BeNull();
        new OverlayToleranceChannel().TryPublish(NamePid(), family).Should().BeNull();
    }

    [Fact]
    public void TheDescriptorIsTheUsersReadAndNothingElse()
    {
        var user = new SecurityIdentifier("S-1-5-21-1-2-3-1001");
        OverlayToleranceChannel.Sddl(user).Should().Be("O:S-1-5-21-1-2-3-1001D:P(A;;GR;;;S-1-5-21-1-2-3-1001)");
    }

    /// <summary>The port's one name as the guard ABI takes it: NUL-terminated UTF-8, or nothing for a name that could split the list.</summary>
    [Fact]
    public void TheGuardIsHandedOneTerminatedNameOrNone()
    {
        NativeAntiCheatGuard.ToleranceBytes("NetEase Yidun").Should().Equal([.. Encoding.UTF8.GetBytes("NetEase Yidun"), (byte)0]);
        NativeAntiCheatGuard.ToleranceBytes(null).Should().BeNull();
        NativeAntiCheatGuard.ToleranceBytes(" ").Should().BeNull();
        NativeAntiCheatGuard.ToleranceBytes("NetEase Yidun\nEasy Anti-Cheat").Should().BeNull("a newline would name a second family nobody granted");
    }
}

using FluentAssertions;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Tests.AntiCheat;

/// <summary>
/// D33 (owner decision 2026-09-26): the managed half of the user-mode exception can only narrow. A family is named to the
/// guard only when the option is on, a grant is on the row for these bytes, and the block is a module, file or folder
/// finding of that family; a game is eligible only when the guard let exactly that family through and the evidence
/// reaches the owner's two sessions.
/// </summary>
public sealed class UserModeExceptionRulesTests
{
    private const string _block = "AntiCheatFile|NetEase Yidun|NEP2.dll";

    private static ExecutableFingerprint OnDisk => new() { ExePath = @"C:\Games\GF2\GF2_Exilium.exe", SizeBytes = 10, MtimeUnixMs = 20 };

    private static GameConsentRecord Record(string? block = _block, AntiCheatExceptionGrant? grant = null) =>
        GameConsentRecord.Stored(OnDisk, hookEnabled: true, DateTimeOffset.UnixEpoch, ConsentProvenance.ConsentDialog, "consent-dialog/1", block,
            preScanUnverified: false, DateTimeOffset.UnixEpoch, grant);

    private static AntiCheatExceptionGrant Grant(string family = "NetEase Yidun", long size = 10, long mtime = 20) => new()
    {
        Family = family,
        GrantedAt = DateTimeOffset.UnixEpoch,
        DisclosureVersion = "ac-exception-dialog/1",
        ExeSizeBytes = size,
        ExeMtimeUnixMs = mtime,
    };

    [Fact]
    public void TheFamilyIsNamedOnlyWhenEveryConditionHolds()
    {
        UserModeExceptionRules.CoveredFamily(Record(grant: Grant()), OnDisk, optionOn: true).Should().Be("NetEase Yidun");

        UserModeExceptionRules.CoveredFamily(Record(grant: Grant()), OnDisk, optionOn: false).Should().BeNull("off suspends every exception");
        UserModeExceptionRules.CoveredFamily(Record(), OnDisk, optionOn: true).Should().BeNull("no grant, no exception");
        UserModeExceptionRules.CoveredFamily(Record(grant: Grant(size: 11)), OnDisk, optionOn: true).Should().BeNull("a game update ends it");
        UserModeExceptionRules.CoveredFamily(Record(grant: Grant(family: "Anybrain")), OnDisk, optionOn: true).Should().BeNull("the grant is for the block's family");
        UserModeExceptionRules.CoveredFamily(Record(block: "BlockedStoreId|NetEase Yidun|steam:1", grant: Grant()), OnDisk, optionOn: true)
            .Should().BeNull("a title list is never excepted");
        UserModeExceptionRules.CoveredFamily(Record(block: null, grant: Grant()), OnDisk, optionOn: true).Should().BeNull("an exception exists only over a block");
        UserModeExceptionRules.CoveredFamily(default, OnDisk, optionOn: true).Should().BeNull("a record nobody stored excepts nothing");
    }

    [Fact]
    public void EligibleMeansTheGuardLetThatFamilyThroughAndTwoSessions()
    {
        StoredBlock? block = StoredBlock.Parse(_block);
        AntiCheatVerdict through = AntiCheatVerdict.AllowedUnderException("NetEase Yidun", "NEP2.dll");

        UserModeExceptionRules.IsEligible(block, through, sessions: 2).Should().BeTrue();
        UserModeExceptionRules.IsEligible(block, through, sessions: 1).Should().BeFalse("the owner's threshold is two");
        UserModeExceptionRules.IsEligible(block, AntiCheatVerdict.Allowed(), sessions: 3).Should().BeFalse("a plain pass let nothing through, so it confirmed nothing");
        UserModeExceptionRules.IsEligible(block, AntiCheatVerdict.AllowedUnderException("Anybrain", "x.dll"), sessions: 3).Should().BeFalse();
        UserModeExceptionRules.IsEligible(block, AntiCheatVerdict.Refused(AntiCheatRefusalReason.AntiCheatFile, "Kernel driver in the game folder", "NEPKernel.sys"),
            sessions: 3).Should().BeFalse("the Aniimo shape");
        UserModeExceptionRules.IsEligible(StoredBlock.Parse("BlockedExecutable|Valve VAC|cs2.exe"), through, sessions: 3).Should().BeFalse();
        UserModeExceptionRules.IsEligible(null, through, sessions: 3).Should().BeFalse();
        UserModeExceptionRules.RequiredSessions.Should().Be(2);
    }

    [Fact]
    public void AStoredBlockIsReadOnlyInItsThreeSlotShape()
    {
        StoredBlock.Parse(_block).Should().Be(new StoredBlock(AntiCheatRefusalReason.AntiCheatFile, "NetEase Yidun", "NEP2.dll"));
        StoredBlock.Parse("AntiCheatFile: NetEase Yidun NEP2.dll").Should().BeNull("a row from before 2026-09-25 cannot be split back apart");
        StoredBlock.Parse("3|NetEase Yidun|NEP2.dll").Should().BeNull("a number is not a reason's name");
        StoredBlock.Parse("antichEatfile|NetEase Yidun|NEP2.dll").Should().BeNull("names are exact");
        StoredBlock.Parse(null).Should().BeNull();
        StoredBlock.Parse("AntiCheatDirectory|NetEase Yidun|Yidun|x")!.Value.Signal.Should().Be("Yidun|x", "the signal is the last slot");
        new StoredBlock(AntiCheatRefusalReason.BlockedModule, string.Empty, "x.dll").IsExceptionable.Should().BeFalse("a finding that names no family");
    }
}

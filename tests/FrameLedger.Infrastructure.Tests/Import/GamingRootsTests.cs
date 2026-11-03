// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text;
using FluentAssertions;
using FrameLedger.Infrastructure.Import;

namespace FrameLedger.Infrastructure.Tests.Import;

/// <summary>
/// beta.33: the drives' <c>.GamingRoot</c> files as the owner's are (<c>spike-notes</c> §25) — C: 28 bytes naming
/// <c>XboxGames</c>, D: 18 bytes naming <c>xbox</c> — and every way one can fail to be one, costing that drive or that name
/// only; and Windows' own package answers, which are the same on every PC for a given identity.
/// </summary>
public sealed class GamingRootsTests
{
    private static byte[] Bytes(uint count, params string[] names)
    {
        List<byte> bytes = [.. "RGBX"u8.ToArray(), .. BitConverter.GetBytes(count)];
        foreach (string name in names)
        {
            bytes.AddRange(Encoding.Unicode.GetBytes(name + "\0"));
        }

        return [.. bytes];
    }

    [Fact]
    public void TheOwnersTwoFilesNameTheirGamesFolders()
    {
        byte[] c = Bytes(1, "XboxGames");
        byte[] d = Bytes(1, "xbox");
        c.Should().HaveCount(28, "the C: file measured on the owner's PC");
        d.Should().HaveCount(18, "the D: file measured on the owner's PC");

        GamingRoots.Parse(c, @"C:\").Should().Equal(@"C:\XboxGames");
        GamingRoots.Parse(d, @"D:\").Should().Equal(@"D:\xbox");
        GamingRoots.Parse(Bytes(2, "XboxGames", @"More\Games"), @"E:\").Should().Equal(@"E:\XboxGames", @"E:\More\Games");
    }

    [Fact]
    public void AFileThatIsNotOneNamesNothing()
    {
        GamingRoots.Parse([], @"C:\").Should().BeEmpty();
        GamingRoots.Parse("XGBR\u0001\0\0\0x\0\0\0"u8.ToArray(), @"C:\").Should().BeEmpty("the magic is RGBX");
        GamingRoots.Parse(Bytes(0), @"C:\").Should().BeEmpty();
        GamingRoots.Parse(Bytes((uint)GamingRoots.MaxCount + 1, "x"), @"C:\").Should().BeEmpty("a count no install has is a file that is not one");
    }

    [Fact]
    public void ATruncatedFileKeepsTheNamesItHoldsAndAStrangeNameCostsItself()
    {
        byte[] truncated = [.. Bytes(2, "XboxGames"), .. Encoding.Unicode.GetBytes("Cut")];
        GamingRoots.Parse(truncated, @"C:\").Should().Equal(@"C:\XboxGames");

        GamingRoots.Parse(Bytes(5, @"..\Windows", @"C:\Elsewhere", "Fine", "a:b", "."), @"D:\").Should().ContainSingle().Which.Should().Be(@"D:\Fine",
            "only a plain folder on the file's own drive is a games folder");
    }

    [Fact]
    public void WindowsComputesTheFamilyNameTheStartMenuLists()
    {
        // The identity Valor Mortis' MicrosoftGame.config states, and the family name Get-AppxPackage and Get-StartApps list
        // for it on the owner's PC; PackageFamilyNameFromId computes it the same way on any Windows.
        WindowsInstalledPackages.Instance.FamilyName("LyricalGamesLLC.ValorMortis", "CN=C32D4B9A-1AEC-416A-8124-789B35AD6753")
            .Should().Be("LyricalGamesLLC.ValorMortis_72fag5ekhk96y");
        WindowsInstalledPackages.Instance.IsInstalled("FrameLedger.NotAPackage_0000000000000").Should().BeFalse();
    }
}

using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using FrameLedger.Domain.Detection;
using FrameLedger.Infrastructure.Detection;

namespace FrameLedger.Infrastructure.Tests.Detection;

/// <summary>
/// Unreal Engine's build facts read from a shipping executable ON DISK (beta.10): where the branch name is looked for, what a
/// cut or a malformed file costs, and which executable is read. Synthetic PEs only — the owner's games were measured, not
/// copied (<c>05_DETECTION</c> §Engine version).
/// </summary>
public sealed class UnrealBuildReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static byte[] Wide(string text) => Encoding.Unicode.GetBytes(text);

    [Fact]
    public void TheBranchNameInRdataIsTheReleaseLine()
    {
        byte[] pe = SyntheticPe.Build((".text", new byte[512]), (".rdata", Padded(Wide("++UE5+Release-5.1-CL-0"), 512)));

        UnrealBuildReader.ScanBranches(new MemoryStream(pe), Ct).Should().Equal("5.1");
    }

    /// <summary>A PE with an engine-sized <c>.rdata</c> is read there and only there: a name in another section is not looked for.</summary>
    [Fact]
    public void APeWithAnEngineSizedRdataIsNotScannedOutsideIt()
    {
        byte[] pe = SyntheticPe.Build((".text", Padded(Wide("++UE4+Release-4.27"), 512)), (".rdata", new byte[(int)UnrealBuildReader.MinRdataBytes]));

        UnrealBuildReader.ScanBranches(new MemoryStream(pe), Ct).Should().BeEmpty();
    }

    /// <summary>
    /// Black Myth: Wukong's shape — a protected executable whose <c>.rdata</c> is 8 KB (or empty) holds its constants
    /// elsewhere, and is scanned whole.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(8192)]
    public void AProtectedPeWithATinyRdataIsScannedWhole(int rdataBytes)
    {
        byte[] pe = SyntheticPe.Build((".code", Padded(Wide("++UE5+Release-5.0-CL-0"), 1024)), (".rdata", new byte[rdataBytes]));

        UnrealBuildReader.ScanBranches(new MemoryStream(pe), Ct).Should().Equal("5.0");
    }

    [Fact]
    public void BytesThatAreNotAPeAreScannedWhole()
    {
        byte[] bytes = [.. new byte[100], .. Wide("++UE4+Release-4.26"), .. new byte[100]];

        UnrealBuildReader.ScanBranches(new MemoryStream(bytes), Ct).Should().Equal("4.26");
    }

    /// <summary>
    /// A name cut by a chunk's edge is read whole from the next window — never as the digits that fitted ("4.2" of "4.27").
    /// </summary>
    [Fact]
    public void ANameStraddlingTwoChunksIsReadWholeNotTruncated()
    {
        byte[] name = Wide("++UE4+Release-4.27-CL-0");
        foreach (int cut in new[] { 4, 30, 31, 34 })
        {
            byte[] bytes = new byte[UnrealBuildReader.ChunkBytes + 256];
            name.CopyTo(bytes, UnrealBuildReader.ChunkBytes - cut);

            UnrealBuildReader.ScanBranches(new MemoryStream(bytes), Ct).Should().Equal(["4.27"], $"the chunk edge cut the name {cut} bytes in");
        }
    }

    [Theory]
    [InlineData("++UE5+Release-6.0")]      // the UE5 prefix, a line no UE5 has
    [InlineData("++UE4+Release-4.99")]
    [InlineData("++UE5+Release-")]         // nothing after the prefix
    [InlineData("+UE5+Release-5.1")]       // one plus: not the engine's branch name
    public void AnythingButAnEngineReleaseLineIsNotOne(string text)
    {
        byte[] bytes = [.. new byte[64], .. Wide(text), .. new byte[64]];

        UnrealBuildReader.ScanBranches(new MemoryStream(bytes), Ct).Should().BeEmpty();
    }

    [Fact]
    public void AnEmptyStreamNamesNothing() =>
        UnrealBuildReader.ScanBranches(new MemoryStream([]), Ct).Should().BeEmpty();

    [Fact]
    public void ACancelledScanStopsBetweenChunks()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        ((Action)(() => UnrealBuildReader.ScanBranches(new MemoryStream(new byte[1024]), cancelled.Token)))
            .Should().Throw<OperationCanceledException>();
    }

    /// <summary>The fixture corpus' shape: a zero-byte "shipping executable" is no facts worth a version, never a throw.</summary>
    [Fact]
    public void AZeroByteExecutableYieldsNoVersion()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fl-unreal-{Guid.NewGuid():N}-Win64-Shipping.exe");
        File.WriteAllBytes(path, []);
        try
        {
            UnrealBuildFacts? facts = UnrealBuildReader.Read(path, "Game-Win64-Shipping.exe", Ct);

            facts.Should().NotBeNull();
            UnrealVersion.Decide(facts!).Should().Be(EngineVersionReading.None);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AMissingExecutableIsNoFacts() =>
        UnrealBuildReader.Read(Path.Combine(Path.GetTempPath(), $"fl-missing-{Guid.NewGuid():N}.exe"), null, Ct).Should().BeNull();

    /// <summary>The row's own executable when it is the shipping build; else the largest shipping executable the walk listed.</summary>
    [Fact]
    public void TheShippingExecutableIsTheRowsOwnOrTheLargestListed()
    {
        string root = Path.Combine(Path.GetTempPath(), $"fl-ue-{Guid.NewGuid():N}");
        string small = Path.Combine(root, "Tool", "Binaries", "Win64", "Tool-Win64-Shipping.exe");
        string large = Path.Combine(root, "Game", "Binaries", "Win64", "Game-Win64-Shipping.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(small)!);
        Directory.CreateDirectory(Path.GetDirectoryName(large)!);
        File.WriteAllBytes(small, new byte[10]);
        File.WriteAllBytes(large, new byte[20]);
        File.WriteAllBytes(Path.Combine(root, "Game.exe"), new byte[5]);
        try
        {
            string dir = root.Replace('\\', '/');
            string[] listing = ["Game.exe", "Tool/Binaries/Win64/Tool-Win64-Shipping.exe", "Game/Binaries/Win64/Game-Win64-Shipping.exe"];

            GameFileProbe.ShippingExecutable(Snapshot(dir + "/Game.exe", dir, listing))
                .Should().Be((dir + "/Game/Binaries/Win64/Game-Win64-Shipping.exe", "Game/Binaries/Win64/Game-Win64-Shipping.exe"));
            GameFileProbe.ShippingExecutable(Snapshot(dir + "/Tool/Binaries/Win64/Tool-Win64-Shipping.exe", dir, listing))
                .Should().Be((dir + "/Tool/Binaries/Win64/Tool-Win64-Shipping.exe", "Tool/Binaries/Win64/Tool-Win64-Shipping.exe"), "the row's own shipping build wins");
            GameFileProbe.ShippingExecutable(Snapshot(dir + "/Game.exe", dir, ["Game.exe"])).Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static GameFileSnapshot Snapshot(string exe, string dir, IReadOnlyList<string> files) => new()
    {
        ExePath = exe,
        ExeNameWithoutExtension = Path.GetFileNameWithoutExtension(exe),
        GameDirectory = dir,
        RelativeFiles = files,
        RelativeDirectories = [],
        FileListingComplete = true,
        SiblingFileVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        MatchedStringNeedles = new HashSet<string>(StringComparer.Ordinal),
        StringsRegexCaptures = new Dictionary<string, string>(StringComparer.Ordinal),
        ManifestFields = new Dictionary<string, string>(StringComparer.Ordinal),
        UncollectedFacts = new HashSet<DetectionSignalType>(),
    };

    private static byte[] Padded(byte[] content, int size)
    {
        byte[] section = new byte[Math.Max(size, content.Length)];
        content.CopyTo(section, 16);
        return section;
    }

    /// <summary>A PE32+ with named sections laid out one after another on disk; an empty section has no raw bytes.</summary>
    private static class SyntheticPe
    {
        public static byte[] Build(params (string Name, byte[] Raw)[] sections)
        {
            const int peAt = 0x80;
            const int optionalSize = 240;
            int headersEnd = peAt + 24 + optionalSize + (40 * sections.Length);
            int raw = (headersEnd + 0x1FF) & ~0x1FF;
            int total = raw + sections.Sum(static s => s.Raw.Length);
            byte[] file = new byte[total];
            file[0] = (byte)'M';
            file[1] = (byte)'Z';
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(60), peAt);
            file[peAt] = (byte)'P';
            file[peAt + 1] = (byte)'E';
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(peAt + 4), ExecutableArchitecture.MachineAmd64);
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(peAt + 6), (ushort)sections.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(peAt + 20), optionalSize);
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(peAt + 24), 0x20B);

            int header = peAt + 24 + optionalSize;
            int cursor = raw;
            for (int i = 0; i < sections.Length; i++)
            {
                (string name, byte[] bytes) = sections[i];
                Encoding.ASCII.GetBytes(name).CopyTo(file, header);
                BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(header + 8), (uint)bytes.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(header + 12), (uint)(0x1000 * (i + 1)));
                BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(header + 16), (uint)bytes.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(header + 20), bytes.Length == 0 ? 0u : (uint)cursor);
                bytes.CopyTo(file, cursor);
                cursor += bytes.Length;
                header += 40;
            }

            return file;
        }
    }
}

using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using FrameLedger.Domain.Detection;
using FrameLedger.Infrastructure.Detection;

namespace FrameLedger.Infrastructure.Tests.Detection;

/// <summary>
/// The import-table reader behind the "is Vulkan" fact (P4 PR-2), driven by synthetic PEs built in memory: a
/// link-time import is read from the import directory and the delay-load directory, a <c>LoadLibraryW</c> wide
/// literal is found by the probe's strings pass, a malformed file costs an answer and never an exception, and a
/// file that is not a PE reads as unknown rather than "no".
/// </summary>
public sealed class PeImportsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void ALinkTimeImportIsReadFromTheImportDirectory()
    {
        using var pe = new MemoryStream(SyntheticPe.Build(imports: ["KERNEL32.dll", "vulkan-1.dll"], delayImports: []));
        IReadOnlySet<string>? names = PeImports.Read(pe);
        names.Should().NotBeNull();
        names!.Count.Should().Be(2);
        names.Contains("VULKAN-1.DLL").Should().BeTrue("the set is case-insensitive: the file's spelling is kept, the lookup does not care");
        names.Contains("kernel32.dll").Should().BeTrue();
    }

    [Fact]
    public void ADelayLoadImportCountsToo()
    {
        using var pe = new MemoryStream(SyntheticPe.Build(imports: ["KERNEL32.dll"], delayImports: ["vulkan-1.dll"]));
        PeImports.Read(pe)!.Contains("vulkan-1.dll").Should().BeTrue();
    }

    [Fact]
    public void NotAPeReadsAsUnknownAndAMalformedPeCostsAnAnswerNotAnException()
    {
        using var text = new MemoryStream(Encoding.ASCII.GetBytes("this is not a portable executable"));
        PeImports.Read(text).Should().BeNull();

        byte[] good = SyntheticPe.Build(imports: ["vulkan-1.dll"], delayImports: []);
        // Point the import directory past the end of the file: the walk must refuse rather than throw.
        byte[] truncated = good[..(good.Length / 2)];
        using var bad = new MemoryStream(truncated);
        Func<IReadOnlySet<string>?> read = () => PeImports.Read(bad);
        read.Should().NotThrow().Which.Should().BeNull();
    }

    [Fact]
    public async Task TheProbeReportsTheLoaderFromAnImportOrAWideLiteralAndUnknownForAnUnreadableFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "fl-pe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var probe = new GameFileProbe();
            var rules = new DetectionRuleSet { SchemaVersion = 2, RulesVersion = "t", Engines = [], Platforms = [], Capabilities = [] };

            string linked = Path.Combine(dir, "linked.exe");
            await File.WriteAllBytesAsync(linked, SyntheticPe.Build(imports: ["vulkan-1.dll"], delayImports: []), Ct);
            (await probe.SnapshotAsync(linked, rules, Ct)).VulkanLoaderReferenced.Should().BeTrue("a link-time import");

            string wide = Path.Combine(dir, "wide.exe");
            await File.WriteAllBytesAsync(wide, SyntheticPe.Build(imports: ["KERNEL32.dll"], delayImports: [], trailing: Encoding.Unicode.GetBytes("vulkan-1.dll")), Ct);
            (await probe.SnapshotAsync(wide, rules, Ct)).VulkanLoaderReferenced.Should().BeTrue("LoadLibraryW(L\"vulkan-1.dll\") — the harness's shape, seen through the UTF-16 view");

            string d3d = Path.Combine(dir, "d3d.exe");
            await File.WriteAllBytesAsync(d3d, SyntheticPe.Build(imports: ["d3d11.dll", "dxgi.dll"], delayImports: []), Ct);
            (await probe.SnapshotAsync(d3d, rules, Ct)).VulkanLoaderReferenced.Should().BeFalse("a readable PE that names no loader anywhere");

            string missing = Path.Combine(dir, "gone.exe");
            (await probe.SnapshotAsync(missing, rules, Ct)).VulkanLoaderReferenced.Should().BeNull("could not look is not \"no\"");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The smallest PE32+ this reader accepts: one section holding the import and delay-load directories and their names.</summary>
    private static class SyntheticPe
    {
        public static byte[] Build(IReadOnlyList<string> imports, IReadOnlyList<string> delayImports, byte[]? trailing = null)
        {
            const uint sectionRva = 0x1000;
            const uint sectionRaw = 0x400;
            const int importDescriptorSize = 20;
            const int delayDescriptorSize = 32;

            // Section layout: import descriptors, delay descriptors, then the name strings, then any trailing bytes.
            int importsBytes = (imports.Count + 1) * importDescriptorSize;
            int delayBytes = (delayImports.Count + 1) * delayDescriptorSize;
            var names = new List<(string Name, int Offset)>();
            int cursor = importsBytes + delayBytes;
            foreach (string n in imports.Concat(delayImports))
            {
                names.Add((n, cursor));
                cursor += n.Length + 1;
            }

            int trailingAt = cursor;
            int sectionSize = cursor + (trailing?.Length ?? 0);
            byte[] section = new byte[sectionSize];
            for (int i = 0; i < imports.Count; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(section.AsSpan(i * importDescriptorSize + 12), sectionRva + (uint)names[i].Offset);
            }

            for (int i = 0; i < delayImports.Count; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(section.AsSpan(importsBytes + i * delayDescriptorSize + 4), sectionRva + (uint)names[imports.Count + i].Offset);
            }

            foreach ((string name, int offset) in names)
            {
                Encoding.ASCII.GetBytes(name).CopyTo(section, offset);
            }

            trailing?.CopyTo(section, trailingAt);

            byte[] file = new byte[sectionRaw + sectionSize];
            WriteHeaders(file, sectionRva, sectionRaw, sectionSize, importsBytes, delayImports.Count > 0 ? delayBytes : 0);
            section.CopyTo(file, (int)sectionRaw);
            return file;
        }

        /// <summary>DOS header, PE signature, a PE32+ optional header whose directories 1 and 13 point into the one section, and that section's header.</summary>
        private static void WriteHeaders(byte[] file, uint sectionRva, uint sectionRaw, int sectionSize, int importsBytes, int delayBytes)
        {
            // DOS header: "MZ", e_lfanew at 60 → 0x80.
            file[0] = (byte)'M';
            file[1] = (byte)'Z';
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(60), 0x80);
            // PE signature + file header: 1 section, optional header size 240 (PE32+).
            file[0x80] = (byte)'P';
            file[0x81] = (byte)'E';
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(0x84 + 2), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(0x84 + 16), 240);
            int optional = 0x84 + 20;
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(optional), 0x20B);
            int directories = optional + 112;
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(directories + 1 * 8), sectionRva);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(directories + 1 * 8 + 4), (uint)importsBytes);
            if (delayBytes > 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(directories + 13 * 8), sectionRva + (uint)importsBytes);
                BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(directories + 13 * 8 + 4), (uint)delayBytes);
            }

            // One section header right after the optional header.
            int sectionHeader = optional + 240;
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(sectionHeader + 8), (uint)sectionSize);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(sectionHeader + 12), sectionRva);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(sectionHeader + 16), (uint)sectionSize);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(sectionHeader + 20), sectionRaw);
        }
    }
}

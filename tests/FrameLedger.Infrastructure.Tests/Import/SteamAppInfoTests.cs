// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using FrameLedger.Infrastructure.Import;

namespace FrameLedger.Infrastructure.Tests.Import;

/// <summary>
/// What the Steam app's own file says of controller support (owner decision D163): read from files written here to
/// the layout of format 41 - which was checked against the developer's own Steam folder on 2026-11-14 - and, where a
/// Steam app is installed on the machine that runs the tests, from the real one.
/// </summary>
public sealed class SteamAppInfoTests
{
    private const uint _magic41 = 0x07564429;

    // The table of strings: a tree names its keys by their number here.
    private static readonly string[] _keys = ["appinfo", "appid", "common", "name", "controller_support", "extended", "depots"];

    private static byte[] Key(string name) => BitConverter.GetBytes(Array.IndexOf(_keys, name));

    private static byte[] Map(string key, params byte[][] children) => [0x00, .. Key(key), .. children.SelectMany(static c => c), 0x08];

    private static byte[] Text(string key, string value) => [0x01, .. Key(key), .. Encoding.UTF8.GetBytes(value), 0];

    private static byte[] Number(string key, int value) => [0x02, .. Key(key), .. BitConverter.GetBytes(value)];

    private static byte[] Wide(string key, string value) => [0x05, .. Key(key), .. Encoding.Unicode.GetBytes(value), 0, 0];

    private static byte[] Long(string key, byte kind, long value) => [kind, .. Key(key), .. BitConverter.GetBytes(value)];

    /// <summary>A record's tree as Steam writes one: the root map, and the end mark after it.</summary>
    private static byte[] Tree(uint appId, string? support, params byte[][] more)
    {
        byte[][] common = support is null ? [Text("name", "A Game")] : [Text("name", "A Game"), Text("controller_support", support)];
        byte[][] children = [Number("appid", (int)appId), Map("common", common), .. more];
        return [.. Map("appinfo", children), 0x08];
    }

    /// <summary>A whole file: the header, each record with its sixty bytes of state, the zero id, the table of strings.</summary>
    private static byte[] File(uint magic, IReadOnlyList<(uint AppId, byte[] Tree)> records, string[]? keys = null)
    {
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(magic));
        bytes.AddRange(BitConverter.GetBytes(1u));
        bytes.AddRange(new byte[8]);
        foreach ((uint appId, byte[] tree) in records)
        {
            bytes.AddRange(BitConverter.GetBytes(appId));
            bytes.AddRange(BitConverter.GetBytes((uint)(60 + tree.Length)));
            bytes.AddRange(Enumerable.Repeat((byte)0xAB, 60));
            bytes.AddRange(tree);
        }

        bytes.AddRange(BitConverter.GetBytes(0u));
        byte[] file = [.. bytes];
        BinaryPrimitives.WriteInt64LittleEndian(file.AsSpan(8), file.Length);
        string[] table = keys ?? _keys;
        return [.. file, .. BitConverter.GetBytes((uint)table.Length), .. table.SelectMany(static k => Encoding.UTF8.GetBytes(k).Append((byte)0))];
    }

    private static byte[] Library() => File(_magic41,
    [
        (10, Tree(10, "full")),
        (20, Tree(20, "partial", Map("extended", Text("name", "x"), Number("appid", 7)))),
        (30, Tree(30, null)),
        (40, Tree(40, "Full ")),
        (50, Tree(50, "something a later Steam writes")),
    ]);

    [Fact]
    public void ARecordSaysFullPartialOrNothingAndAnIdWithNoRecordIsNotAnswered()
    {
        SteamAppInfoResult read = SteamAppInfo.ReadControllerSupport(Library(), [10, 20, 30, 40, 50, 999]);

        read.Problem.Should().BeNull();
        read.Support.Should().BeEquivalentTo(new Dictionary<uint, string>
        {
            [10] = SteamAppInfo.Full,
            [20] = SteamAppInfo.Partial,
            [30] = SteamAppInfo.None,
            [40] = SteamAppInfo.Full,
            [50] = SteamAppInfo.None,
        });
        read.Support.Should().NotContainKey(999u, "the store's file does not describe it: that is not 'none'");
    }

    [Fact]
    public void OnlyTheApplicationsAskedAboutAreAnswered()
    {
        SteamAppInfo.ReadControllerSupport(Library(), [20]).Support.Should().ContainSingle().Which.Should().Be(new KeyValuePair<uint, string>(20, SteamAppInfo.Partial));
        SteamAppInfo.ReadControllerSupport(Library(), []).Support.Should().BeEmpty();
    }

    /// <summary>The key is only the store's word under the record's own "common" section: the same key anywhere else in the tree says nothing.</summary>
    [Fact]
    public void TheKeyCountsOnlyInTheRecordsCommonSection()
    {
        byte[] elsewhere = [.. Map("appinfo", Number("appid", 1), Map("common", Text("name", "A")), Map("extended", Text("controller_support", "full"), Map("common", Text("controller_support", "full")))), 0x08];
        byte[] atTheRoot = [.. Map("appinfo", Text("controller_support", "full"), Map("common", Text("name", "A"))), 0x08];

        SteamAppInfoResult read = SteamAppInfo.ReadControllerSupport(File(_magic41, [(1, elsewhere), (2, atTheRoot)]), [1, 2]);

        read.Problem.Should().BeNull();
        read.Support.Should().BeEquivalentTo(new Dictionary<uint, string> { [1] = SteamAppInfo.None, [2] = SteamAppInfo.None });
    }

    [Fact]
    public void EveryKindOfValueATreeCanHoldIsPassedOver()
    {
        byte[] tree =
        [
            .. Map("appinfo",
                Number("appid", 1),
                [0x03, .. Key("depots"), 0, 0, 128, 63],
                [0x04, .. Key("depots"), 1, 2, 3, 4],
                [0x06, .. Key("depots"), 255, 0, 0, 255],
                Long("depots", 0x07, 123456789012345),
                Long("depots", 0x0A, -5),
                Wide("depots", "wide"),
                Map("depots", Map("extended", Map("depots"))),
                Map("common", Text("controller_support", "partial"))),
            0x0B,
        ];

        SteamAppInfo.ReadControllerSupport(File(_magic41, [(1, tree)]), [1]).Support[1].Should().Be(SteamAppInfo.Partial);
    }

    [Fact]
    public void AFileOfAnotherFormatIsRefusedWithTheReason()
    {
        SteamAppInfoResult read = SteamAppInfo.ReadControllerSupport(File(0x07564428, [(10, Tree(10, "full"))]), [10]);

        read.Support.Should().BeEmpty();
        read.Problem.Should().Be("format 0x07564428 is not the one this build reads (0x07564429)");
    }

    [Fact]
    public void AFileWhoseTableHasNeitherKeyIsReadAndEveryRecordListsNone()
    {
        byte[] file = File(_magic41, [(10, Tree(10, "full"))], keys: ["a", "b", "c", "d", "e", "f", "g"]);

        SteamAppInfoResult read = SteamAppInfo.ReadControllerSupport(file, [10]);

        read.Problem.Should().BeNull();
        read.Support[10].Should().Be(SteamAppInfo.None);
    }

    /// <summary>No length in the file is trusted: cut anywhere, it is a problem and never a read past the end - and never half an answer.</summary>
    [Fact]
    public void AFileCutAtAnyByteIsAProblemOrAWholeAnswerAndNeverThrows()
    {
        byte[] whole = Library();
        SteamAppInfo.ReadControllerSupport(whole, [10, 20, 30]).Support.Should().HaveCount(3);

        for (int cut = 0; cut < whole.Length; cut++)
        {
            SteamAppInfoResult read = SteamAppInfo.ReadControllerSupport(whole.AsSpan(0, cut), [10, 20, 30]);

            (read.Problem is not null || read.Support.Count == 3).Should().BeTrue($"cut at {cut}: a problem, or the whole answer");
            if (read.Problem is not null)
            {
                read.Support.Should().BeEmpty($"cut at {cut}: a file that cannot be read whole says nothing");
            }
        }
    }

    [Theory]
    [InlineData(4u)] // smaller than its own state
    [InlineData(0xFFFFFFFFu)] // larger than the file
    public void ARecordWhoseSizeIsNotPossibleIsAProblem(uint size)
    {
        byte[] file = Library();
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16 + 4), size);

        SteamAppInfoResult read = SteamAppInfo.ReadControllerSupport(file, [10]);

        read.Problem.Should().Be("a record claims more than the file holds");
        read.Support.Should().BeEmpty();
    }

    [Fact]
    public void ATreeThatIsNotWellFormedIsAProblemOnlyForARecordThatIsAskedAbout()
    {
        byte[] unknownKind = [0x00, .. Key("appinfo"), 0x7F, .. Key("appid"), 1, 2, 3, 4, 0x08, 0x08];
        byte[] unendedString = [0x00, .. Key("appinfo"), 0x01, .. Key("name"), (byte)'x'];
        byte[] unendedMap = [0x00, .. Key("appinfo"), 0x00, .. Key("common")];
        byte[] file = File(_magic41, [(1, unknownKind), (2, unendedString), (3, unendedMap), (4, Tree(4, "full"))]);

        SteamAppInfo.ReadControllerSupport(file, [4]).Support[4].Should().Be(SteamAppInfo.Full, "the records before it are stepped over by their size");
        foreach (uint bad in new uint[] { 1, 2, 3 })
        {
            SteamAppInfo.ReadControllerSupport(file, [bad, 4]).Problem.Should().Be("a record's tree cannot be read", $"record {bad}");
        }
    }

    [Fact]
    public void ATreeNestedDeeperThanAnyRecordIsRefusedRatherThanFollowed()
    {
        byte[] deep = Map("common");
        for (int i = 0; i < 40; i++)
        {
            deep = Map("extended", deep);
        }

        SteamAppInfo.ReadControllerSupport(File(_magic41, [(1, [.. deep, 0x08])]), [1]).Problem.Should().Be("a record's tree cannot be read");
    }

    [Fact]
    public void AFileThatIsNotThereOrIsEmptyIsAProblemAndNotAnException()
    {
        string dir = Path.Combine(Path.GetTempPath(), "fl-appinfo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            SteamAppInfo.ReadControllerSupport(Path.Combine(dir, "absent.vdf"), [10]).Problem.Should().Be(nameof(FileNotFoundException));
            string empty = Path.Combine(dir, "empty.vdf");
            System.IO.File.WriteAllBytes(empty, []);
            SteamAppInfo.ReadControllerSupport(empty, [10]).Problem.Should().Be("the file is empty or too large");
            string good = Path.Combine(dir, "appinfo.vdf");
            System.IO.File.WriteAllBytes(good, Library());
            SteamAppInfo.ReadControllerSupport(good, [10]).Support[10].Should().Be(SteamAppInfo.Full);
            SteamAppInfo.ReadControllerSupport(new byte[8], [10]).Problem.Should().Be("shorter than a header");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// The real file, where a Steam app is installed for the user running the tests - CI's runner has none, and then
    /// there is nothing to assert. It asserts the CONTRACT: the file this build's Steam client wrote is read without a
    /// problem, and every answer is one of the three words.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task TheSteamAppsOwnFileOnThisMachineIsReadWhereThereIsOne()
    {
        if (SteamAppInfo.InstalledFile() is not { } path || !System.IO.File.Exists(path))
        {
            return;
        }

        uint[] installed = [.. (await new SteamLibrarySource().ListAsync(TestContext.Current.CancellationToken))
            .Select(static g => uint.TryParse(g.StoreId, out uint id) ? id : 0).Where(static id => id != 0)];

        SteamAppInfoResult read = SteamAppInfo.ReadControllerSupport(path, installed);

        if (read.Problem is { } problem && problem.StartsWith("format ", StringComparison.Ordinal))
        {
            // A Steam client that writes another format: the reader says so and the fact reads N/A. Not this test's to fail on.
            return;
        }

        read.Problem.Should().BeNull();
        read.Support.Values.Should().BeSubsetOf([SteamAppInfo.Full, SteamAppInfo.Partial, SteamAppInfo.None]);
        ILookup<string, uint> by = read.Support.ToLookup(static p => p.Value, static p => p.Key, StringComparer.Ordinal);
        TestContext.Current.SendDiagnosticMessage($"Steam's appinfo.vdf: {read.Support.Count} of {installed.Length} installed applications have a record; "
            + $"full {by[SteamAppInfo.Full].Count()}, partial {by[SteamAppInfo.Partial].Count()}, none {by[SteamAppInfo.None].Count()}");
    }
}

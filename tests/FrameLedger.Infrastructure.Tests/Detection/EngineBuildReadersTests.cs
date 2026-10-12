// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using FrameLedger.Domain.Detection;
using FrameLedger.Infrastructure.Detection;

namespace FrameLedger.Infrastructure.Tests.Detection;

/// <summary>
/// beta.19's three readers over files the test writes in the shapes measured on the owner's installs (2026-10-12): a Unity
/// data file's header, a Godot pack beside the executable and one appended to it, RPG Maker MZ/MV core scripts and a VX Ace
/// <c>Game.ini</c> — and since beta.20 Ren'Py's <c>renpy/vc_version.py</c>. Each reads a bounded head of one file and says
/// nothing when the file is not what it expects.
/// </summary>
public sealed class EngineBuildReadersTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-enginebuild-" + Guid.NewGuid().ToString("N"));

    public EngineBuildReadersTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Sub(params string[] parts)
    {
        string p = Path.Combine([_dir, .. parts]);
        Directory.CreateDirectory(p);
        return p;
    }

    private static byte[] PackHeader(uint format, uint major, uint minor, uint patch)
    {
        byte[] header = new byte[GodotBuildReader.HeaderBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(header, GodotBuildReader.PackMagic);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), format);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), major);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), minor);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), patch);
        return header;
    }

    [Theory]
    [InlineData(20, "2019.4.1f1")]
    [InlineData(48, "2022.3.39f1")]
    public async Task AUnityDataFilesHeaderNamesItsEditorVersion(int offset, string version)
    {
        // The serialized-file header: numbers, then the version as a NUL-terminated string (at 20 in a 2019.4 build, 48 later).
        byte[] bytes = new byte[4096];
        Encoding.ASCII.GetBytes(version + "\0").CopyTo(bytes, offset);
        string file = Path.Combine(Sub("Game_Data"), "globalgamemanagers");
        await File.WriteAllBytesAsync(file, bytes, Ct);

        UnityBuildReader.DataHeaderVersion(file).Should().Be(version);
        UnityBuildFacts? facts = UnityBuildReader.Read(playerPath: null, file);
        facts.Should().NotBeNull();
        UnityVersion.Decide(facts!).Should().Be(new EngineVersionReading(version, EngineVersionSource.UnityData));
    }

    [Fact]
    public async Task AnEncryptedOrAbsentUnityDataFileSaysNothing()
    {
        string file = Path.Combine(Sub("GF2_Data"), "globalgamemanagers");
        await File.WriteAllBytesAsync(file, [.. Enumerable.Range(0, 4096).Select(static i => (byte)((i * 37) + 11))], Ct);

        UnityBuildReader.DataHeaderVersion(file).Should().BeNull("GIRLS' FRONTLINE 2's data file carries no readable header");
        UnityBuildReader.DataHeaderVersion(Path.Combine(_dir, "missing")).Should().BeNull();
        UnityBuildReader.Read(null, null).Should().BeNull();
    }

    [Fact]
    public void AGodotPackHeaderIsReadAndAnythingElseIsNot()
    {
        GodotBuildReader.ReadHeader(new MemoryStream(PackHeader(2, 4, 2, 2)), 0).Should().Equal([4, 2, 2]);
        byte[] wrong = PackHeader(2, 4, 2, 2);
        wrong[0] = (byte)'X';
        GodotBuildReader.ReadHeader(new MemoryStream(wrong), 0).Should().BeNull("not GDPC");
        GodotBuildReader.ReadHeader(new MemoryStream(new byte[10]), 0).Should().BeNull("shorter than a header");
    }

    [Fact]
    public void APackAppendedToTheExecutableIsFoundThroughItsTrailer()
    {
        using var exe = new MemoryStream();
        exe.Write(new byte[5000]);
        byte[] pack = [.. PackHeader(2, 4, 4, 1), .. new byte[300]];
        exe.Write(pack);
        Span<byte> trailer = stackalloc byte[GodotBuildReader.TrailerBytes];
        BinaryPrimitives.WriteInt64LittleEndian(trailer, pack.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(trailer[8..], GodotBuildReader.PackMagic);
        exe.Write(trailer);

        GodotBuildReader.ReadAppended(exe).Should().Equal([4, 4, 1]);

        BinaryPrimitives.WriteInt64LittleEndian(trailer, exe.Length * 2);
        exe.Position = exe.Length - GodotBuildReader.TrailerBytes;
        exe.Write(trailer);
        GodotBuildReader.ReadAppended(exe).Should().BeNull("a size larger than the file points at nothing");
        GodotBuildReader.ReadAppended(new MemoryStream(new byte[100])).Should().BeNull("no trailer");
    }

    [Fact]
    public async Task AGodotPackBesideTheExecutableIsPreferredToTheExecutablesEnd()
    {
        string game = Sub("plethora-of-bullets-2");
        string exe = Path.Combine(game, "POB2v020beta5.exe");
        await File.WriteAllBytesAsync(exe, new byte[64], Ct);
        await File.WriteAllBytesAsync(Path.Combine(game, "POB2v020beta5.pck"), [.. PackHeader(2, 4, 2, 2), .. new byte[64]], Ct);

        GodotBuildFacts? facts = GodotBuildReader.Read(exe, Path.Combine(game, "POB2v020beta5.pck"));

        facts.Should().NotBeNull();
        facts!.PackVersion.Should().Equal([4, 2, 2]);
        facts.PackEmbedded.Should().BeFalse();
        GodotBuildReader.Read(exe, packPath: null).Should().BeNull("a zero-filled executable has no appended pack and no version resource");
    }

    [Fact]
    public async Task MzAndMvAreReadFromTheirCoreScriptsBesideOrAboveTheExecutable()
    {
        // A Doll's Locket - Remake: the MZ runtime and its own copy of the scripts in css\.
        string css = Sub("Dolls Locket", "css");
        string js = Sub("Dolls Locket", "css", "js");
        await File.WriteAllBytesAsync(Path.Combine(css, "Game.exe"), new byte[8], Ct);
        await File.WriteAllTextAsync(Path.Combine(js, "rmmz_core.js"),
            "//=============================================================================\r\n// rmmz_core.js v1.10.0\r\n//=============================================================================\r\n", Ct);
        RpgMakerBuildReader.Read(Path.Combine(css, "Game.exe")).Should().Be(new RpgMakerBuildFacts { CoreScript = "rmmz_core.js", CoreScriptVersion = "1.10.0" });

        // HELLO, HELLO WORLD!: the runtime in swiftshader\, the MV scripts one folder up, under www\.
        string runtime = Sub("HELLO, HELLO WORLD!", "swiftshader");
        string www = Sub("HELLO, HELLO WORLD!", "www", "js");
        await File.WriteAllBytesAsync(Path.Combine(runtime, "Game.exe"), new byte[8], Ct);
        await File.WriteAllTextAsync(Path.Combine(www, "rpg_core.js"), "//====\n// rpg_core.js v1.6.1\n//====\n", Ct);
        RpgMakerBuildFacts? mv = RpgMakerBuildReader.Read(Path.Combine(runtime, "Game.exe"));
        mv.Should().Be(new RpgMakerBuildFacts { CoreScript = "rpg_core.js", CoreScriptVersion = "1.6.1" });
    }

    [Fact]
    public async Task VxAceIsReadFromTheLibraryItsGameIniNames()
    {
        string game = Sub("City of Bones 1.0");
        string system = Sub("City of Bones 1.0", "System");
        await File.WriteAllBytesAsync(Path.Combine(game, "CityofBones.exe"), new byte[8], Ct);
        await File.WriteAllTextAsync(Path.Combine(game, "Game.ini"), "[Game]\r\nRTP=RPGVXAce\r\nLibrary=System\\RGSS301.dll\r\nScripts=Data\\Scripts.rvdata2\r\nTitle=City of Bones\r\n", Ct);
        await File.WriteAllBytesAsync(Path.Combine(system, "RGSS301.dll"), new byte[8], Ct);

        RpgMakerBuildReader.LibraryOf(Path.Combine(game, "Game.ini")).Should().Be(Path.Combine(system, "RGSS301.dll"));
        RpgMakerBuildFacts? facts = RpgMakerBuildReader.Read(Path.Combine(game, "CityofBones.exe"));
        facts.Should().Be(new RpgMakerBuildFacts { RgssLibrary = "RGSS301.dll" }, "a library with no version resource is named, and its name carries 3.0.1");
        RpgMakerVersion.Decide(facts!).Should().Be(new EngineVersionReading("3.0.1", EngineVersionSource.Rgss3Library));
    }

    [Fact]
    public async Task ALibraryOutsideTheGamesFolderIsNamedNotReadAndNoMarkerIsNoFacts()
    {
        string game = Sub("Elsewhere");
        await File.WriteAllTextAsync(Path.Combine(game, "Game.ini"), "[Game]\nLibrary=..\\..\\Windows\\RGSS103J.dll\n", Ct);

        RpgMakerBuildReader.LibraryOf(Path.Combine(game, "Game.ini")).Should().Be("RGSS103J.dll");

        string plain = Sub("a", "b", "c", "Plain");
        await File.WriteAllBytesAsync(Path.Combine(plain, "Plain.exe"), new byte[8], Ct);
        RpgMakerBuildReader.Read(Path.Combine(plain, "Plain.exe")).Should().BeNull();
        RpgMakerBuildReader.Read(Path.Combine(plain, "Plain.exe"), rgssLibrary: Path.Combine(plain, "RGSS202E.dll"))
            .Should().Be(new RpgMakerBuildFacts { RgssLibrary = "RGSS202E.dll" }, "the walk's own find when no Game.ini names one");
    }

    /// <summary>
    /// beta.20: Ren'Py's version line, from the engine's <c>renpy/vc_version.py</c> beside the launcher, in the shape measured
    /// on Bound Between Desks — and nothing from a folder without the file, from a file whose lines name no version
    /// (<c>version_name</c> is not <c>version</c>), or from past the bounded head.
    /// </summary>
    [Fact]
    public async Task RenPysVersionLineIsReadFromTheEngineBesideTheLauncher()
    {
        string game = Sub("Bound Between Desks");
        string engine = Sub("Bound Between Desks", "renpy");
        string exe = Path.Combine(game, "BoundBetweenDesks.exe");
        string versionFile = Path.Combine(engine, "vc_version.py");
        await File.WriteAllBytesAsync(exe, new byte[8], Ct);

        RenPyBuildReader.Read(exe).Should().BeNull("no version file");

        await File.WriteAllTextAsync(versionFile, "branch = 'master'\nnightly = False\nofficial = True\nversion = '8.5.0.25111603'\nversion_name = 'In Good Health'\n", Ct);
        RenPyBuildFacts? facts = RenPyBuildReader.Read(exe);
        facts.Should().Be(new RenPyBuildFacts { VcVersion = "8.5.0.25111603" });
        RenPyVersion.Decide(facts!).Should().Be(new EngineVersionReading("8.5.0", EngineVersionSource.RenPyVcVersion));

        await File.WriteAllTextAsync(versionFile, "version_name = 'In Good Health'\nnightly = False\n", Ct);
        RenPyBuildReader.Read(exe).Should().BeNull("a version name is not a version");

        await File.WriteAllTextAsync(versionFile, new string('#', RenPyBuildReader.VersionFileBytes) + "\nversion = '8.5.0.1'\n", Ct);
        RenPyBuildReader.Read(exe).Should().BeNull("only the bounded head is read");
    }
}

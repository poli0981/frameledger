// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Infrastructure.Detection;

namespace FrameLedger.Infrastructure.Tests.Detection;

/// <summary>
/// The install-root cases both resolvers read (beta.33): <c>tests/fixtures/install-roots/cases.txt</c>, answered here by
/// <see cref="InstallRoot.Resolve"/> and in <c>fl_guard_test</c> by the native <c>ResolveInstallRoot</c>. They had drifted:
/// the managed list carried <c>GamesInstalled</c> from #17 and the native one never did. A GDK package root is marked by
/// FILES (<c>MicrosoftGame.config</c> beside <c>appxmanifest.xml</c>, <c>spike-notes</c> §25), so each case builds its tree
/// in a folder of its own under the temp directory.
/// </summary>
public sealed class InstallRootCasesTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "fl-install-roots-" + Guid.NewGuid().ToString("N"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FrameLedger.slnx")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the cases live in the repository");
        return dir.FullName;
    }

    public static TheoryData<string, string, string> Cases()
    {
        var data = new TheoryData<string, string, string>();
        foreach (string line in File.ReadAllLines(Path.Combine(RepoRoot(), "tests", "fixtures", "install-roots", "cases.txt")))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            string[] fields = line.Split('|');
            fields.Should().HaveCount(3, $"'{line}' must be <files>|<exe>|<root>");
            data.Add(fields[0], fields[1], fields[2]);
        }

        return data;
    }

    [Fact]
    public void TheListIsNotEmpty() =>
        Cases().Should().HaveCountGreaterThanOrEqualTo(10, "an empty list is a green theory that compared nothing");

    [Theory]
    [MemberData(nameof(Cases))]
    public void EachSharedCaseResolvesToItsRoot(string files, string exe, string root)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(exe);
        ArgumentNullException.ThrowIfNull(root);
        Directory.CreateDirectory(_temp);
        foreach (string item in files.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string path = Path.Combine(_temp, item);
            if (item.AsSpan().EndsWith('\\'))
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, []);
            }
        }

        InstallRoot.Resolve(Path.Combine(_temp, exe)).Should().Be(Path.Combine(_temp, root), $"the case for {exe}");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_temp))
            {
                Directory.Delete(_temp, recursive: true);
            }
        }
        catch (IOException)
        {
            // A folder still in use is the OS's to clean; the case already answered.
        }
    }
}

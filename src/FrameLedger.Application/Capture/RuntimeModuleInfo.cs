// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Capture;

/// <summary>One vendor runtime module the loader reported in the target, with its file version.</summary>
/// <remarks>
/// <b>The version is read from the FILE on disk, never from the process.</b> The only thing read
/// out of the target is the loader's module list — the same documented read the anti-cheat guard
/// performs on every target before injecting — and the path it names is then opened as a file.
/// CLAUDE.md rule 4 is untouched: no game memory, no vendor object, no hook.
/// </remarks>
public sealed record RuntimeModuleInfo(string FileName, string Path, string? FileVersion, Version? Parsed);

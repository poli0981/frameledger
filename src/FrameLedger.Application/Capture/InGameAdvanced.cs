// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Shared;

namespace FrameLedger.Application.Capture;

/// <summary>
/// One publication of the shared memory's region 5 — what the hooked game told its upscaler, as the Overlay's watchdog
/// last published it for this session's request (<c>07_IPC</c> §Region 5; Advanced metrics train B).
/// </summary>
/// <remarks>
/// A class around the struct, made once a publication and not once a tick: the region is 1472 bytes and published once
/// a second, the loop ticks ten times as often, and <see cref="CaptureProgress"/> is a new record every tick. The same
/// instance on two ticks is the same publication, which is how a consumer tells "nothing new" without comparing.
/// </remarks>
public sealed class InGameAdvanced
{
    private readonly FlAdvancedState _state;

    public InGameAdvanced(FlAdvancedState state) => _state = state;

    /// <summary>The publication, by reference: it is never copied to be read.</summary>
    public ref readonly FlAdvancedState State => ref _state;
}

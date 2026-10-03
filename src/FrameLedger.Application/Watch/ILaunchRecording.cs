// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Watch;

/// <summary>One launch's recorder; <see cref="Description"/> is what the log says about the environment it got.</summary>
public interface ILaunchRecording : IDisposable
{
    ISessionRecorder Recorder { get; }

    string Description { get; }
}

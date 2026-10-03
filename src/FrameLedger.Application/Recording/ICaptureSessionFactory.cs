// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Capture;

namespace FrameLedger.Application.Recording;

/// <summary>
/// Builds the session the recorder drives, with the recorder as its observer. The composition root
/// decides every other collaborator (the gate, the guard, the adapters); the recorder decides only that
/// it is listening.
/// </summary>
public interface ICaptureSessionFactory
{
    CaptureSession Create(ICaptureObserver observer);
}

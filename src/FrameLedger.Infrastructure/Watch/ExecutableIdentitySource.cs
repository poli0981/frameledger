// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;
using FrameLedger.Infrastructure.Io;

namespace FrameLedger.Infrastructure.Watch;

/// <summary>The orchestrator's view of the file on disk: <see cref="ExecutableIdentity.Read"/> behind the port.</summary>
public sealed class ExecutableIdentitySource : IExecutableIdentitySource
{
    /// <inheritdoc />
    public string Normalise(string exePath) => ExecutableIdentity.Normalise(exePath);

    public ExecutableFingerprint? Read(string normalisedExePath) => ExecutableIdentity.Read(normalisedExePath);
}

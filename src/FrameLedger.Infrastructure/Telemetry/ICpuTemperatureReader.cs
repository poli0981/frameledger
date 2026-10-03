// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>A CPU package temperature, where something privileged enough to read one exists; null when this tick had none.</summary>
public interface ICpuTemperatureReader : IDisposable
{
    double? Read();
}

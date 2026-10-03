// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Update;

/// <summary>What the uninstall hook did, for its log line and its test.</summary>
public sealed record UninstallOutcome(bool AgentAsked, bool LayerUnregistered, bool TaskRemoved, bool DataAsked, bool DataDeleted);

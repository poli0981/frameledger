// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Capture;

namespace FrameLedger.Application.Ipc;

/// <summary>
/// D33 (owner decision 2026-09-26): what <see cref="AgentCommandHandler"/> needs to answer for a game's user-mode exception —
/// the option and the disclosure version this Agent grants against. One parameter rather than two, so a composition either
/// has the exception or does not. (D38 took the session count out: a grant needs no session any more.)
/// </summary>
/// <param name="Switch"><c>hooking.usermode_ac_exceptions</c>, read on every command.</param>
/// <param name="DisclosureVersion"><c>Shared.Safety.AntiCheatExceptionDisclosure.Version</c> under <c>--serve</c>.</param>
public sealed record AntiCheatExceptionCommands(IUserModeExceptionSwitch Switch, string DisclosureVersion);

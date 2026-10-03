// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.Shared.Safety;
using SafetyStrings = FrameLedger.Shared.Strings;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The admin mode's disclosure (beta.10, owner decision D34; <c>19_SAFETY</c> §Elevated targets): what running the Agent as
/// administrator adds, what it does not change, how Windows asks, and the risk of a per-user install. <see cref="Accepted"/>
/// — the user ticking that they understand — is the one thing that enables the dialog's primary button.
/// </summary>
public sealed partial class AgentAdminDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _accepted;

    public static string Title => SafetyStrings.Safety_AdminMode_Title;

    public static string Intro => SafetyStrings.Safety_AdminMode_Intro;

    public static string Adds => SafetyStrings.Safety_AdminMode_Adds;

    public static string Unchanged => SafetyStrings.Safety_AdminMode_Unchanged;

    public static string Prompt => SafetyStrings.Safety_AdminMode_Prompt;

    public static string Risk => SafetyStrings.Safety_AdminMode_Risk;

    public static string AcceptText => SafetyStrings.Safety_AdminMode_Accept;

    public static string TurnOnText => SafetyStrings.Safety_AdminMode_TurnOn;

    public static string CancelText => SafetyStrings.Safety_AdminMode_Cancel;

    /// <summary>The version of the text shown, in the dialog's corner, as FR-2.1's dialog shows its own.</summary>
    public static string Version => AgentAdminDisclosure.Version;
}

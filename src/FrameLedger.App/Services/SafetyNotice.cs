// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>One persistent safety notice: what happened, in plain language, with the signal named; dismissed by the user and never by a timeout.</summary>
public sealed record SafetyNotice(SafetyNoticeKind Kind, string Title, string Body, DateTimeOffset At)
{
    public bool IsError => Kind is SafetyNoticeKind.Refused or SafetyNoticeKind.Unhooked or SafetyNoticeKind.HookingOff;

    public InfoBarSeverity Severity => IsError ? InfoBarSeverity.Error : InfoBarSeverity.Warning;

    /// <summary>The one action FR-14 allows on a refusal — acknowledging that the session is recorded without measuring — and a plain Dismiss otherwise (no session is running for the others).</summary>
    public string ActionText => Kind == SafetyNoticeKind.Refused ? Shared.Strings.Safety_RecordWithoutMeasuring : Strings.Notice_Dismiss;

    /// <summary>The notice in one sentence (beta.35, D88): the list item's name, and its button's help text — what it acknowledges.</summary>
    public string AccessibleName => SpokenText.TitleAndBody(Title, Body);

    public override string ToString() => AccessibleName;
}

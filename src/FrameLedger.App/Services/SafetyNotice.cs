using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>One persistent safety notice: what happened, in plain language, with the signal named; dismissed by the user and never by a timeout.</summary>
public sealed record SafetyNotice(SafetyNoticeKind Kind, string Title, string Body, DateTimeOffset At)
{
    public bool IsError => Kind is SafetyNoticeKind.Refused or SafetyNoticeKind.Unhooked;

    public InfoBarSeverity Severity => IsError ? InfoBarSeverity.Error : InfoBarSeverity.Warning;

    /// <summary>The one action FR-14 allows on a refusal — acknowledging that the session is recorded without measuring — and a plain Dismiss otherwise.</summary>
    public string ActionText => Kind == SafetyNoticeKind.Refused ? Shared.Strings.Safety_RecordWithoutMeasuring : Strings.Notice_Dismiss;
}

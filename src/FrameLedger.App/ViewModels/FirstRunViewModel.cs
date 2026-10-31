// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Services;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// 08_UI §First-run flow: (1) the Legal Gate — four documents, one Accept, Decline exits; (2) Agent setup —
/// unelevated by default, elevation optional, the capability facts as the Agent reports them; (3) the hooking
/// explainer — Tier 1 and Tier 2 in the same reviewed paragraphs the consent dialog uses, and that Tier 2
/// measures nothing; (4) the import placeholder. In read-only mode (Settings ▸ Reopen) only step 1 shows and
/// nothing is written.
/// </summary>
public sealed partial class FirstRunViewModel : ObservableObject, IDisposable
{
    public const int LegalStep = 0;
    public const int AgentStep = 1;
    public const int ExplainerStep = 2;
    public const int ImportStep = 3;

    private readonly LegalGate _gate;
    private readonly IAgentLink _agent;
    private readonly IUrlOpener _urls;
    private readonly UiThread _ui = new();
    private readonly TaskCompletionSource<bool> _outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLegalStep))]
    [NotifyPropertyChangedFor(nameof(IsAgentStep))]
    [NotifyPropertyChangedFor(nameof(IsExplainerStep))]
    [NotifyPropertyChangedFor(nameof(IsImportStep))]
    [NotifyPropertyChangedFor(nameof(StepText))]
    private int _step = LegalStep;

    [ObservableProperty]
    private LegalDocument? _selectedDocument;

    [ObservableProperty]
    private string _agentState = Strings.Agent_State_Connecting;

    [ObservableProperty]
    private string _agentVersion = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _telemetrySource = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _overlayBuildId = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _cpuTemperature = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _elevated = Strings.Common_NotAvailable;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private bool _isBusy;

    /// <summary>The Alpha notice's own tick box (D74): Accept waits for it whenever an alpha's gate is shown.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private bool _alphaAcknowledged;

    public FirstRunViewModel(LegalGate gate, IAgentLink agent, bool readOnly, IUrlOpener? urls = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _urls = urls ?? new ShellUrlOpener();
        ReadOnly = readOnly;
        AlphaTickRequired = !readOnly && gate.Documents.Any(static d => d.RequiresAcceptance && string.Equals(d.Key, LegalDocuments.Alpha, StringComparison.Ordinal));
        SelectedDocument = gate.Documents.Count > 0 ? gate.Documents[0] : null;
        _agent.Changed += OnAgentChanged;
        Refresh();
    }

    /// <summary>Settings ▸ Reopen: the documents only, nothing written.</summary>
    public bool ReadOnly { get; }

    /// <summary>
    /// An alpha build's gate (D74, 2026-10-31): the Alpha notice is among the documents to accept, so its own warning and
    /// tick box are shown above them, and Accept waits for the tick — every time an alpha's gate is shown, not only the
    /// first, because the user is accepting the notice again with whatever moved.
    /// </summary>
    public bool AlphaTickRequired { get; }

    /// <summary>The alpha's warning, in the user's language; it summarises, the notice is the text accepted.</summary>
    public static string AlphaSummary => Strings.FirstRun_Alpha_Summary;

    public static string AlphaTick => Strings.FirstRun_Alpha_Tick;

    public IReadOnlyList<LegalDocument> Documents => _gate.Documents;

    /// <summary>The opener the rendered documents' links leave the App through (beta.12) — the same one View on GitHub uses.</summary>
    public IUrlOpener Urls => _urls;

    /// <summary>True when the user accepted (and the rows are written), false when they declined or closed the window.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "the task of this view model's own TaskCompletionSource, completed by the window's buttons on the dispatcher; FirstRunFlow awaits it for the user's answer, and RunContinuationsAsynchronously keeps the completion from running the waiter inline (VS Threading Analyzers 18.12 flag it; Dependabot #314)")]
    public Task<bool> Outcome => _outcome.Task;

    public bool IsLegalStep => Step == LegalStep;

    public bool IsAgentStep => Step == AgentStep;

    public bool IsExplainerStep => Step == ExplainerStep;

    public bool IsImportStep => Step == ImportStep;

    public string StepText => Step switch
    {
        AgentStep => Strings.FirstRun_Agent_Header,
        ExplainerStep => Strings.FirstRun_Explainer_Header,
        ImportStep => Strings.FirstRun_Import_Header,
        _ => Strings.FirstRun_Legal_Header,
    };

    public static string Title => Strings.FirstRun_Title;

    /// <summary>The plain-language summary above the documents (2026-09-21): beta, minimum requirements, the ban risk, your data. It summarises; it is not the terms.</summary>
    public static string LegalSummary => Strings.FirstRun_Legal_Summary;

    public static string LegalBody => Strings.FirstRun_Legal_Body;

    public static string AgentBody => Strings.FirstRun_Agent_Body;

    public static string ExplainerIntro => Strings.FirstRun_Explainer_Intro;

    public static string ExplainerHooked => Shared.Strings.Safety_Consent_Hooked;

    public static string ExplainerNotHooked => Shared.Strings.Safety_Consent_NotHooked;

    public static string ExplainerOff => Strings.FirstRun_Explainer_Off;

    public static string ImportBody => Strings.FirstRun_Import_Body;

    /// <summary>A pending accept, for a test to await.</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    public void Dispose() => _agent.Changed -= OnAgentChanged;

    /// <summary>The window closed without a decision: a decline (FR-11 blocks until accepted).</summary>
    public void Abandon() => _outcome.TrySetResult(false);

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private Task AcceptAsync() => Pending = AcceptCoreAsync();

    private bool CanAccept() => !IsBusy && (!AlphaTickRequired || AlphaAcknowledged);

    [RelayCommand]
    private void Decline() => _outcome.TrySetResult(false);

    [RelayCommand]
    private void Next()
    {
        if (Step < ImportStep)
        {
            Step++;
        }
    }

    [RelayCommand]
    private void Back()
    {
        if (Step > AgentStep)
        {
            Step--;
        }
    }

    [RelayCommand]
    private void Finish() => _outcome.TrySetResult(true);

    /// <summary>Read-only mode's only button.</summary>
    [RelayCommand]
    private void Close() => _outcome.TrySetResult(true);

    /// <summary>The document on GitHub at this build's source, through the App's opener (beta.12: it started a process itself).</summary>
    [RelayCommand]
    private void OpenOnline()
    {
        if (SelectedDocument is not null)
        {
            _ = _urls.Open(SelectedDocument.Url);
        }
    }

    private async Task AcceptCoreAsync()
    {
        if (ReadOnly)
        {
            _outcome.TrySetResult(true);
            return;
        }

        IsBusy = true;
        try
        {
            await _gate.AcceptAllAsync().ConfigureAwait(true);
            if (AlphaTickRequired)
            {
                // D74: an alpha's Agent waits for the notice, and the App held its start while the gate was open
                // (AlphaStart.HoldTheAgentForTheNoticeAsync); the row is written, so start it now rather than at the next round.
                _agent.SetNoticeHold(false);
                _agent.RetryNow();
            }

            Step = AgentStep;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnAgentChanged(object? sender, EventArgs e) => _ui.Post(Refresh);

    private void Refresh()
    {
        AgentState = AgentStatusPresentation.Pill(_agent.State).Text;
        HelloAck? hello = _agent.Hello;
        AgentVersion = hello?.AgentVersion ?? Strings.Common_NotAvailable;
        TelemetrySource = hello?.TelemetrySource ?? Strings.Common_NotAvailable;
        OverlayBuildId = hello?.OverlayBuildId ?? Strings.Common_NotAvailable;
        CpuTemperature = hello is null ? Strings.Common_NotAvailable : hello.CpuTempAvailable ? Strings.Common_Yes : Strings.Common_No;
        Elevated = hello is null ? Strings.Common_NotAvailable : hello.Elevated ? Strings.Common_Yes : Strings.Common_No;
    }
}

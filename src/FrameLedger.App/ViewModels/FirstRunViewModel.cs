using System.Diagnostics;
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
    private bool _isBusy;

    public FirstRunViewModel(LegalGate gate, IAgentLink agent, bool readOnly)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        ReadOnly = readOnly;
        SelectedDocument = gate.Documents.Count > 0 ? gate.Documents[0] : null;
        _agent.Changed += OnAgentChanged;
        Refresh();
    }

    /// <summary>Settings ▸ Reopen: the documents only, nothing written.</summary>
    public bool ReadOnly { get; }

    public IReadOnlyList<LegalDocument> Documents => _gate.Documents;

    /// <summary>True when the user accepted (and the rows are written), false when they declined or closed the window.</summary>
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

    [RelayCommand]
    private Task AcceptAsync() => Pending = AcceptCoreAsync();

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

    [RelayCommand]
    private void OpenOnline()
    {
        if (SelectedDocument is null)
        {
            return;
        }

        try
        {
            using Process? _ = Process.Start(new ProcessStartInfo(SelectedDocument.Url.ToString()) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "first run: could not open the document online");
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

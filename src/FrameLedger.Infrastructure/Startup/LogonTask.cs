using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Startup;

/// <summary>
/// The Agent's "start at logon" scheduled task through the Task Scheduler COM API (<c>Schedule.Service</c>, late
/// bound — no interop assembly): folder <c>\FrameLedger</c>, this user's logon trigger, interactive token, lowest
/// run level (<c>TASK_RUNLEVEL_LUA</c> — no tier needs elevation, ADR-9), the action <c>FrameLedger.Agent.exe --serve</c>,
/// no execution time limit, allowed on battery. A task rather than a Run entry because the Agent must outlive the
/// App; a task rather than a service because a service is machine-wide and elevated.
/// </summary>
/// <remarks>
/// Measured 2026-09-14 on the dev box (Windows 11, standard user): <c>schtasks /Create /SC ONLOGON</c> answers
/// "Access is denied" whatever <c>/RU</c> says, while the COM API registers the same definition unelevated when
/// the trigger and the principal both name the user and the logon type is the interactive token — which is what
/// <c>Register-ScheduledTask</c> does. So the COM API it is, and the task name is a parameter so a test can
/// create and remove one of its own.
/// </remarks>
public sealed class LogonTask
{
    public const string DefaultTaskName = "FrameLedger.Agent";

    public const string Folder = @"\FrameLedger";

    public const string Arguments = "--serve";

    private const int _taskTriggerLogon = 9;
    private const int _taskActionExec = 0;
    private const int _taskLogonInteractiveToken = 3;
    private const int _taskRunLevelLua = 0;
    private const int _taskCreateOrUpdate = 6;
    private const int _fileNotFound = unchecked((int)0x80070002);

    private readonly string _taskName;

    public LogonTask(string taskName = DefaultTaskName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);
        _taskName = taskName;
    }

    /// <summary>The task's state for <paramref name="agentExePath"/>.</summary>
    public LogonTaskState Query(string agentExePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentExePath);
        try
        {
            dynamic service = Connect();
            dynamic? folder = TryGetFolder(service);
            if (folder is null)
            {
                return LogonTaskState.NotInstalled;
            }

            dynamic task;
            try
            {
                task = folder.GetTask(_taskName);
            }
            catch (Exception ex) when (ex is COMException or FileNotFoundException && ex.HResult == _fileNotFound)
            {
                return LogonTaskState.NotInstalled;
            }

            dynamic actions = task.Definition.Actions;
            string? path = actions.Count >= 1 ? (string?)actions.Item(1).Path : null;
            return string.Equals(path, agentExePath, StringComparison.OrdinalIgnoreCase) ? LogonTaskState.Installed : LogonTaskState.Stale;
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return LogonTaskState.Unknown;
        }
    }

    /// <summary>Creates or rewrites the task for <paramref name="agentExePath"/>; the error text on failure, null on success.</summary>
    public string? Install(string agentExePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentExePath);
        try
        {
            dynamic service = Connect();
            dynamic folder = TryGetFolder(service) ?? service.GetFolder("\\").CreateFolder(Folder);
            string user = Environment.UserDomainName + "\\" + Environment.UserName;

            dynamic definition = service.NewTask(0);
            definition.RegistrationInfo.Description = "FrameLedger capture agent (this user, lowest privileges; records only games you added and enabled).";
            definition.Principal.UserId = user;
            definition.Principal.LogonType = _taskLogonInteractiveToken;
            definition.Principal.RunLevel = _taskRunLevelLua;
            dynamic settings = definition.Settings;
            settings.DisallowStartIfOnBatteries = false;
            settings.StopIfGoingOnBatteries = false;
            settings.ExecutionTimeLimit = "PT0S";
            settings.StartWhenAvailable = true;
            dynamic trigger = definition.Triggers.Create(_taskTriggerLogon);
            trigger.UserId = user;
            dynamic action = definition.Actions.Create(_taskActionExec);
            action.Path = agentExePath;
            action.Arguments = Arguments;
            action.WorkingDirectory = Path.GetDirectoryName(agentExePath);

            folder.RegisterTaskDefinition(_taskName, definition, _taskCreateOrUpdate, null, null, _taskLogonInteractiveToken, null);
            return null;
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return ex.Message;
        }
    }

    /// <summary>Removes the task; null on success or when there was none, the error text otherwise.</summary>
    public string? Remove()
    {
        try
        {
            dynamic service = Connect();
            dynamic? folder = TryGetFolder(service);
            if (folder is null)
            {
                return null;
            }

            try
            {
                folder.DeleteTask(_taskName, 0);
            }
            catch (Exception ex) when (ex is COMException or FileNotFoundException && ex.HResult == _fileNotFound)
            {
                return null;
            }

            return null;
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return ex.Message;
        }
    }

    private static dynamic Connect()
    {
        Type type = Type.GetTypeFromProgID("Schedule.Service", throwOnError: true)!;
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }

    private static dynamic? TryGetFolder(dynamic service)
    {
        try
        {
            return service.GetFolder(Folder);
        }
        catch (Exception ex) when (ex is COMException or FileNotFoundException && ex.HResult == _fileNotFound)
        {
            return null;
        }
    }
}

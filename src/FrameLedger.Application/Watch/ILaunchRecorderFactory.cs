using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Watch;

/// <summary>
/// Launch mode for the orchestrator (P3 PR-1b, <c>LaunchGame</c>): a recorder whose session STARTS the title,
/// with whatever environment the launched process is owed — the Vulkan layer's variables unless FR-2.4's switch
/// is on. The composition root builds it, because the environment needs paths the Application layer does not
/// know; the console's <c>launch</c> verb does the same by hand.
/// </summary>
public interface ILaunchRecorderFactory
{
    /// <summary>A recorder and its environment for one launch of <paramref name="normalisedExePath"/>; disposed when the session is over.</summary>
    ValueTask<ILaunchRecording> PrepareAsync(string normalisedExePath, CancellationToken ct = default);
}

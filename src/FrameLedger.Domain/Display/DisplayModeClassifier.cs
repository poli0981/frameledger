namespace FrameLedger.Domain.Display;

/// <summary>
/// One sample's display mode (beta.10, owner request: "detect the window size and whether the game runs full-screen,
/// borderless or windowed", <c>03_METRICS</c> §Display mode), from what the swap chain said and what the window looks like.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ladder.</b> No window and no exclusive swap chain → <see cref="DisplayMode.NoWindow"/>; a minimised window →
/// <see cref="DisplayMode.Minimized"/>; a recent sample in which the swap chain SAID exclusive →
/// <see cref="DisplayMode.ExclusiveFullscreen"/>; a client area covering its monitor → <see cref="DisplayMode.Borderless"/>
/// when the chain said it was not exclusive, <see cref="DisplayMode.CoversScreen"/> when nothing could say (OpenGL, Vulkan,
/// a game that was not hooked); anything else → <see cref="DisplayMode.Windowed"/>.
/// </para>
/// <para>
/// <b>What "exclusive" means here</b> is what the game asked DXGI for. Windows may still present such a game through its
/// compositor ("fullscreen optimizations"), which only a trace session can see and FrameLedger has none (<c>LIMITATIONS.md</c>);
/// and a game's menu can call a borderless window "Fullscreen" — the swap chain's answer is the one reported.
/// </para>
/// </remarks>
public static class DisplayModeClassifier
{
    public static DisplayObservation Classify(SwapChainView? chain, WindowView? window)
    {
        bool exclusiveKnown = chain is { ExclusiveKnown: true, Fresh: true };
        bool exclusive = exclusiveKnown && chain.GetValueOrDefault().Exclusive;
        string? source = SourceOf(chain, window, exclusiveKnown);
        uint? bufferWidth = chain is { BufferWidth: > 0 } c ? c.BufferWidth : null;
        uint? bufferHeight = chain is { BufferHeight: > 0 } d ? d.BufferHeight : null;
        string? swapEffect = chain?.SwapEffect;

        if (window is not { } w)
        {
            // An exclusive chain with a window we could not read is still exclusive: the chain said so.
            DisplayMode alone = exclusive ? DisplayMode.ExclusiveFullscreen : DisplayMode.NoWindow;
            return new DisplayObservation(alone, alone == DisplayMode.NoWindow ? null : source, null, bufferWidth, bufferHeight, null, null, swapEffect);
        }

        DisplayMode mode = w.Minimized ? DisplayMode.Minimized
            : exclusive ? DisplayMode.ExclusiveFullscreen
            : w.CoversMonitor ? (exclusiveKnown ? DisplayMode.Borderless : DisplayMode.CoversScreen)
            : DisplayMode.Windowed;
        return new DisplayObservation(mode, source, w.Client, bufferWidth, bufferHeight, w.Monitor, w.MonitorHz, swapEffect);
    }

    private static string? SourceOf(SwapChainView? chain, WindowView? window, bool exclusiveKnown) =>
        exclusiveKnown ? DisplaySource.SwapChain
        : chain is { OpenGl: true } ? DisplaySource.OpenGl
        : window is not null ? DisplaySource.Window
        : null;
}

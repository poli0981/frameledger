namespace FrameLedger.Domain.Display;

/// <summary>
/// What the injected component said about the presenting swap chain (shared-memory region 4, <c>07_IPC</c>), as the
/// classifier needs it. Never read from the game's memory: the component asked two getters on the chain the game passed.
/// </summary>
/// <param name="Hwnd">The window the chain presents to; 0 for a composition chain.</param>
/// <param name="ExclusiveKnown">Whether the chain was asked (DXGI only; OpenGL and Vulkan have nothing to ask).</param>
/// <param name="Exclusive">What it said; meaningless unless <paramref name="ExclusiveKnown"/>.</param>
/// <param name="OpenGl">Described from an OpenGL stream.</param>
/// <param name="BufferWidth">The back buffer (DXGI) or client area (OpenGL).</param>
/// <param name="BufferHeight">The back buffer's height.</param>
/// <param name="SwapEffect">The swap effect's name (<c>flip_discard</c>, …), or null when not read.</param>
/// <param name="Fresh">
/// Whether the sample is recent (under 2 s old). A minimised game issues only presents the hook drops before sampling, so a
/// stale sample says nothing about now — its exclusive bits are treated as unknown.
/// </param>
public readonly record struct SwapChainView(
    ulong Hwnd,
    bool ExclusiveKnown,
    bool Exclusive,
    bool OpenGl,
    uint BufferWidth,
    uint BufferHeight,
    string? SwapEffect,
    bool Fresh);

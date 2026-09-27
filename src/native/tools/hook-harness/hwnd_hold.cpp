// hook-harness --hold-presenting-hwnd N: a D3D11 swap chain bound to a REAL window, presented every few milliseconds
// for N seconds (layout 4, 2026-09-27, beta.10). Every other DXGI mode here presents to a composition swap chain, which
// has no window -- so until this mode no test had a chain whose OutputWindow, exclusive state and back buffer region 4
// (fl_shm.h §Region 4) could describe. The window is never shown and the chain stays windowed: exclusive fullscreen
// needs a real output, which a hosted runner has not got, and is the owner's measurement on real hardware.
//
// WARP, so it runs with no GPU. Flip model first (what a modern title uses), the legacy discard model when flip is not
// available. The window, its client size and the swap effect are printed so a test compares region 4 with what the
// fixture made rather than with a number it assumes. Exit codes:
//   0   presented for the whole hold
//   77  this machine cannot run the fixture (no window, no device, no swap chain)

#include <windows.h>

#include <d3d11.h>
#include <dxgi.h>

#include <cstdint>
#include <cstdio>

namespace {

LRESULT CALLBACK HwndHoldWndProc(HWND h, UINT m, WPARAM w, LPARAM l) {
    return DefWindowProcW(h, m, w, l);
}

int Skip(const char* why) {
    std::printf("  [SKIP] %s\n", why);
    std::fflush(stdout);
    return 77;
}

HRESULT MakeChain(HWND hwnd, LONG width, LONG height, DXGI_SWAP_EFFECT effect, UINT buffers, IDXGISwapChain** sc,
                  ID3D11Device** dev, ID3D11DeviceContext** ctx) {
    DXGI_SWAP_CHAIN_DESC desc{};
    desc.BufferDesc.Width = static_cast<UINT>(width);
    desc.BufferDesc.Height = static_cast<UINT>(height);
    desc.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    desc.BufferCount = buffers;
    desc.OutputWindow = hwnd;
    desc.Windowed = TRUE;
    desc.SwapEffect = effect;
    return D3D11CreateDeviceAndSwapChain(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION,
                                         &desc, sc, dev, nullptr, ctx);
}

}    // namespace

int HoldPresentingHwnd(int seconds, int presentIntervalMs) {
    std::printf("\n[hwnd-hold] a D3D11 swap chain on a real (hidden) window, presenting for %d second(s) every %d ms\n",
                seconds, presentIntervalMs);
    std::fflush(stdout);

    WNDCLASSW wc{};
    wc.lpfnWndProc = &HwndHoldWndProc;
    wc.hInstance = GetModuleHandleW(nullptr);
    wc.lpszClassName = L"FrameLedgerHookHarnessHwnd";
    RegisterClassW(&wc);
    const HWND hwnd = CreateWindowExW(0, wc.lpszClassName, L"FrameLedger hook-harness (HWND swap chain)",
                                      WS_OVERLAPPEDWINDOW, 0, 0, 320, 200, nullptr, nullptr, wc.hInstance, nullptr);
    if (hwnd == nullptr) {
        return Skip("no window could be created (no interactive window station?)");
    }
    RECT client{};
    if (!GetClientRect(hwnd, &client) || client.right <= 0 || client.bottom <= 0) {
        DestroyWindow(hwnd);
        return Skip("the window has no client area");
    }

    IDXGISwapChain*      sc = nullptr;
    ID3D11Device*        dev = nullptr;
    ID3D11DeviceContext* ctx = nullptr;
    DXGI_SWAP_EFFECT     effect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
    HRESULT              hr = MakeChain(hwnd, client.right, client.bottom, effect, 2, &sc, &dev, &ctx);
    if (FAILED(hr)) {
        effect = DXGI_SWAP_EFFECT_DISCARD;
        hr = MakeChain(hwnd, client.right, client.bottom, effect, 1, &sc, &dev, &ctx);
    }
    if (FAILED(hr) || sc == nullptr) {
        std::printf("  D3D11CreateDeviceAndSwapChain(WARP, hwnd) failed: 0x%08lX\n", static_cast<unsigned long>(hr));
        DestroyWindow(hwnd);
        return Skip("no D3D11 swap chain for a window on this machine");
    }

    std::printf("  hwnd=0x%llx width=%ld height=%ld swapEffect=%d\n",
                static_cast<unsigned long long>(reinterpret_cast<uintptr_t>(hwnd)), client.right, client.bottom,
                static_cast<int>(effect));
    std::fflush(stdout);

    const ULONGLONG until = GetTickCount64() + static_cast<ULONGLONG>(seconds) * 1000ULL;
    long long       presented = 0;
    MSG             msg{};
    while (GetTickCount64() < until) {
        // A window nobody pumps is still a window, but DXGI's own window hook posts to it; drain the queue so the
        // fixture behaves like a title's message loop rather than a hung one.
        while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        sc->Present(0, 0);
        ++presented;
        Sleep(presentIntervalMs > 0 ? static_cast<DWORD>(presentIntervalMs) : 0u);
    }
    std::printf("  presented=%lld\n", presented);
    std::fflush(stdout);

    ctx->Release();
    dev->Release();
    sc->Release();
    DestroyWindow(hwnd);
    return 0;
}

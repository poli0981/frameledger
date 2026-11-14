// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FrameLedger.Application.Input;
using FrameLedger.Domain.Input;
using FrameLedger.Infrastructure.Io;
using Microsoft.Win32.SafeHandles;

namespace FrameLedger.Infrastructure.Input;

/// <summary>
/// The input reader's Windows side (<c>21_INPUT_DEVICES</c> §The reader): a window that is never shown, registered to
/// receive game controllers' reports while the Agent is in the background; three of XInput's getters; the time of the
/// last keyboard or mouse input; the window in front.
/// </summary>
/// <remarks>
/// <para>
/// <b>It opens no device.</b> Raw input hands the reports over; a handle of FrameLedger's on a controller could make a
/// game's own exclusive open of it fail. <b>It sends nothing to a device</b> and asks none for a feature report (owner
/// decision D159). <b>It registers for game controllers only</b> - <see cref="InputReader.RegisteredUsages"/> - never
/// for a keyboard or a mouse (D158).
/// </para>
/// <para>
/// <b>Reports are read in bulk, once a slice</b> (<c>GetRawInputBuffer</c>), not one message at a time: a DualSense
/// sends some hundreds a second, and the Agent's whole budget is 2.5 % of one core (NFR-2). A report that still comes
/// as a message is read too, so that neither path alone has to be the one that works on a given PC.
/// </para>
/// <para>
/// <b>A device's name is read and dropped.</b> It says whether Windows presents the device through XInput and how it
/// is connected (<see cref="RawDeviceName"/>); it can also carry an address, so it is never kept, logged or returned.
/// </para>
/// <para>
/// <b>No controller was at hand when this was written</b> (owner decision D157). What it does with none connected is
/// under test; what it does with one is owner-only item 48, read from the counts every session stores.
/// </para>
/// </remarks>
public sealed unsafe class Win32InputBackend : IInputBackend
{
    /// <summary>The most messages one drain takes off the queue, and the most reads of the buffer: a slice is short.</summary>
    private const int _maxMessages = 2048;
    private const int _maxBufferReads = 64;

    private const int _bufferBytes = 64 * 1024;
    private const int _maxNameChars = 1024;
    private const uint _success = 0;
    private const uint _failed = unchecked((uint)-1);
    private const string _className = "FrameLedger.InputReader";

    private readonly List<(nint Device, RawController Controller)> _present = [];
    private byte[]? _buffer;
    private MessageWindow? _window;
    private bool _classRegistered;
    private bool _registered;
    private bool _xinputAvailable = true;
    private bool _disposed;

    public bool XInputAvailable => _xinputAvailable;

    public bool Open()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssertLayout();

        // On the pinned heap and so at an address a multiple of eight, which GetRawInputBuffer requires of its buffer.
        _buffer = GC.AllocateUninitializedArray<byte>(_bufferBytes, pinned: true);
        nint instance = Win32Input.GetModuleHandleW(null);
        nint window;
        fixed (char* name = _className)
        {
            var windowClass = new Win32Input.WndClassEx
            {
                Size = (uint)sizeof(Win32Input.WndClassEx),
                WndProc = &WindowProcedure,
                Instance = instance,
                ClassName = name,
            };

            // Zero with the class already there is fine: an earlier reader of this process registered it.
            _classRegistered = Win32Input.RegisterClassExW(&windowClass) != 0;
            window = Win32Input.CreateWindowExW(0, name, name, 0, 0, 0, 0, 0, Win32Input.HwndMessage, 0, instance, null);
        }

        if (window == 0)
        {
            return false;
        }

        _window = new MessageWindow(window);
        _registered = Register(Win32Input.RidevInputSink | Win32Input.RidevDevNotify, window);
        if (_registered)
        {
            ListPresent();
        }

        return _registered;
    }

    public void Drain(IInputBackendSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (_window is null || _buffer is null)
        {
            return;
        }

        // The controllers that were there when the reader opened, once.
        foreach ((nint device, RawController controller) in _present)
        {
            sink.Arrived(device, controller);
        }

        _present.Clear();

        // First who came and went, so that a report is never of a device nobody has named yet.
        Win32Input.Message message;
        for (int i = 0; i < _maxMessages && Win32Input.PeekMessageW(&message, 0, Win32Input.WmInputDeviceChange, Win32Input.WmInputDeviceChange, Win32Input.PmRemove) != 0; i++)
        {
            DeviceChanged(message, sink);
        }

        ReadBuffered(sink);

        // Then whatever is left on the queue. A report that came as a message is read as one; anything else is the
        // window's to answer, which for this window is the default.
        for (int i = 0; i < _maxMessages && Win32Input.PeekMessageW(&message, 0, 0, 0, Win32Input.PmRemove) != 0; i++)
        {
            if (message.Id == Win32Input.WmInputDeviceChange)
            {
                DeviceChanged(message, sink);
            }
            else if (message.Id == Win32Input.WmInput)
            {
                ReadMessage(message.LParam, sink);
            }

            Win32Input.DispatchMessageW(&message);
        }
    }

    public bool TryReadPad(int slot, out XInputReading pad)
    {
        pad = default;
        if (!_xinputAvailable)
        {
            return false;
        }

        Win32Input.XInputState state;
        try
        {
            if (Win32Input.XInputGetState((uint)slot, &state) != _success)
            {
                return false;
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            _xinputAvailable = false;
            return false;
        }

        Win32Input.XInputGamepad g = state.Gamepad;
        pad = new XInputReading(state.PacketNumber, g.Buttons, g.LeftTrigger, g.RightTrigger, g.ThumbLx, g.ThumbLy, g.ThumbRx, g.ThumbRy);
        return true;
    }

    public bool TryReadPadDetails(int slot, out XInputDetails details)
    {
        details = default;
        if (!_xinputAvailable)
        {
            return false;
        }

        try
        {
            Win32Input.XInputCapabilities capabilities;
            if (Win32Input.XInputGetCapabilities((uint)slot, Win32Input.XInputFlagGamepad, &capabilities) != _success)
            {
                return false;
            }

            // A pad whose battery XInput does not answer about - one on Bluetooth is reported not to - still has its
            // wireless flag: type 0 is XInput's own "disconnected", which reads as no battery to show.
            Win32Input.XInputBatteryInformation battery = default;
            bool answered = Win32Input.XInputGetBatteryInformation((uint)slot, Win32Input.BatteryDevTypeGamepad, &battery) == _success;
            details = new XInputDetails((capabilities.Flags & Win32Input.XInputCapsWireless) != 0, answered ? battery.Type : 0, answered ? battery.Level : 0);
            return true;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            _xinputAvailable = false;
            return false;
        }
    }

    public bool TryReadLastInputTick(out uint tick)
    {
        var info = new Win32Input.LastInputInfo { Size = (uint)sizeof(Win32Input.LastInputInfo) };
        bool read = Win32Input.GetLastInputInfo(&info) != 0;
        tick = read ? info.Time : 0;
        return read;
    }

    public uint ForegroundProcessId() => ForegroundWindowProbe.ForegroundProcessId();

    public long ThreadCpuMs()
    {
        long creation;
        long exit;
        long kernel;
        long user;
        return Win32Input.GetThreadTimes(Win32Input.GetCurrentThread(), &creation, &exit, &kernel, &user) != 0
            ? (kernel + user) / TimeSpan.TicksPerMillisecond
            : 0;
    }

    /// <summary>
    /// Ends the registration and destroys the window. Called on the thread that opened it: a window belongs to its
    /// thread, and Windows refuses another's attempt to destroy it.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_registered)
        {
            // Ending a registration names no window (the API refuses one there).
            Register(Win32Input.RidevRemove, target: 0);
            _registered = false;
        }

        _window?.Dispose();
        _window = null;
        if (_classRegistered)
        {
            fixed (char* name = _className)
            {
                _ = Win32Input.UnregisterClassW(name, Win32Input.GetModuleHandleW(null));
            }

            _classRegistered = false;
        }

        _buffer = null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam) => Win32Input.DefWindowProcW(window, message, wParam, lParam);

    /// <summary>The structures are passed to Windows by pointer: a size that is not Windows' is a bug to stop on, not to read with.</summary>
    private static void AssertLayout()
    {
        if (sizeof(Win32Input.WndClassEx) != 80 || sizeof(Win32Input.Message) != 48 || sizeof(Win32Input.RawInputDevice) != 16
            || sizeof(Win32Input.RawInputDeviceListEntry) != 16 || sizeof(Win32Input.RidDeviceInfo) != 32 || sizeof(Win32Input.XInputState) != 16
            || sizeof(Win32Input.XInputCapabilities) != 20 || sizeof(Win32Input.XInputBatteryInformation) != 2 || sizeof(Win32Input.LastInputInfo) != 8
            || nint.Size != 8)
        {
            throw new InvalidOperationException("the input reader's structures are not the size Windows expects of them");
        }
    }

    private static bool Register(uint flags, nint target)
    {
        IReadOnlyList<(ushort UsagePage, ushort Usage)> usages = InputReader.RegisteredUsages;
        Win32Input.RawInputDevice* devices = stackalloc Win32Input.RawInputDevice[usages.Count];
        for (int i = 0; i < usages.Count; i++)
        {
            devices[i] = new Win32Input.RawInputDevice { UsagePage = usages[i].UsagePage, Usage = usages[i].Usage, Flags = flags, Target = target };
        }

        return Win32Input.RegisterRawInputDevices(devices, (uint)usages.Count, (uint)sizeof(Win32Input.RawInputDevice)) != 0;
    }

    private void ListPresent()
    {
        uint count = 0;
        uint entry = (uint)sizeof(Win32Input.RawInputDeviceListEntry);
        if (Win32Input.GetRawInputDeviceList(null, &count, entry) == _failed || count is 0 or > 4096)
        {
            return;
        }

        var list = new Win32Input.RawInputDeviceListEntry[count];
        fixed (Win32Input.RawInputDeviceListEntry* p = list)
        {
            uint written = Win32Input.GetRawInputDeviceList(p, &count, entry);
            if (written == _failed)
            {
                return;
            }

            for (int i = 0; i < written && i < list.Length; i++)
            {
                if (list[i].Type == RawInputBuffer.TypeHid && Describe(list[i].Device) is { } controller)
                {
                    _present.Add((list[i].Device, controller));
                }
            }
        }
    }

    private static void DeviceChanged(Win32Input.Message message, IInputBackendSink sink)
    {
        if (message.WParam == Win32Input.DeviceArrival)
        {
            if (Describe(message.LParam) is { } controller)
            {
                sink.Arrived(message.LParam, controller);
            }
        }
        else if (message.WParam == Win32Input.DeviceRemoval)
        {
            sink.Removed(message.LParam);
        }
    }

    /// <summary>What a device is, when it is a game controller; null for anything else, and for a device that left before it could be asked.</summary>
    private static RawController? Describe(nint device)
    {
        var info = new Win32Input.RidDeviceInfo { Size = (uint)sizeof(Win32Input.RidDeviceInfo) };
        uint size = info.Size;
        uint written = Win32Input.GetRawInputDeviceInfoW(device, Win32Input.RidiDeviceInfo, &info, &size);
        if (written is 0 or _failed || info.Type != RawInputBuffer.TypeHid
            || !InputReader.RegisteredUsages.Contains((info.UsagePage, info.Usage)))
        {
            return null;
        }

        // The name is used for the two facts below and goes no further than this method.
        char* name = stackalloc char[_maxNameChars];
        uint chars = _maxNameChars;
        uint copied = Win32Input.GetRawInputDeviceInfoW(device, Win32Input.RidiDeviceName, name, &chars);
        string? path = copied is 0 or _failed || copied > _maxNameChars ? null : new string(name, 0, (int)copied).TrimEnd('\0');
        return new RawController((int)(info.VendorId & 0xFFFF), (int)(info.ProductId & 0xFFFF), RawDeviceName.IsXInputClass(path), RawDeviceName.ConnectionOf(path));
    }

    private void ReadBuffered(IInputBackendSink sink)
    {
        fixed (byte* buffer = _buffer)
        {
            for (int i = 0; i < _maxBufferReads; i++)
            {
                uint size = _bufferBytes;
                uint blocks = Win32Input.GetRawInputBuffer(buffer, &size, RawInputBuffer.HeaderBytes);
                if (blocks is 0 or _failed)
                {
                    return;
                }

                RawInputBuffer.Walk(new ReadOnlySpan<byte>(buffer, _bufferBytes), (int)Math.Min(blocks, int.MaxValue), sink);
            }
        }
    }

    /// <summary>The message-only window, as a handle that is destroyed once.</summary>
    private sealed class MessageWindow : SafeHandleZeroOrMinusOneIsInvalid
    {
        public MessageWindow(nint window)
            : base(ownsHandle: true) => SetHandle(window);

        protected override bool ReleaseHandle() => Win32Input.DestroyWindow(handle) != 0;
    }

    private void ReadMessage(nint rawInput, IInputBackendSink sink)
    {
        fixed (byte* buffer = _buffer)
        {
            uint size = _bufferBytes;
            uint written = Win32Input.GetRawInputData(rawInput, Win32Input.RidInput, buffer, &size, RawInputBuffer.HeaderBytes);
            if (written is not (0 or _failed) && written <= _bufferBytes)
            {
                RawInputBuffer.Walk(new ReadOnlySpan<byte>(buffer, (int)written), 1, sink);
            }
        }
    }
}

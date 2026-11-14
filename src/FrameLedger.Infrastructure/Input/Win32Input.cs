// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Input;

/// <summary>
/// Every Windows function the input reader calls, declared in one place so that the list can be held
/// (<c>tools/input-readonly-check.ps1</c>, <c>21_INPUT_DEVICES</c> §What is read): raw input for game controllers,
/// three of XInput's getters, the time of the last input, and the message-only window raw input is delivered to.
/// </summary>
/// <remarks>
/// <b>What is not here is not called.</b> No function that sends anything to a device (<c>XInputSetState</c>, a HID
/// output or feature report), none that reads a key or a button of the keyboard or mouse, no hook. The structures are
/// declared by hand, blittable, with the sizes the tests assert - the reader passes them to Windows by pointer.
/// </remarks>
internal static unsafe class Win32Input
{
    public const uint WmInputDeviceChange = 0x00FE;
    public const uint WmInput = 0x00FF;

    /// <summary><c>GIDC_ARRIVAL</c> / <c>GIDC_REMOVAL</c>: <c>WM_INPUT_DEVICE_CHANGE</c>'s wParam.</summary>
    public const nuint DeviceArrival = 1;
    public const nuint DeviceRemoval = 2;

    /// <summary><c>RIDEV_INPUTSINK</c>: reports are delivered while the caller is not in front.</summary>
    public const uint RidevInputSink = 0x00000100;

    /// <summary><c>RIDEV_DEVNOTIFY</c>: a device arriving or leaving is told.</summary>
    public const uint RidevDevNotify = 0x00002000;

    /// <summary><c>RIDEV_REMOVE</c>: ends a registration.</summary>
    public const uint RidevRemove = 0x00000001;

    public const uint RidiDeviceName = 0x20000007;
    public const uint RidiDeviceInfo = 0x2000000B;

    /// <summary><c>RID_INPUT</c>: <c>GetRawInputData</c> returns the whole block.</summary>
    public const uint RidInput = 0x10000003;

    public const uint PmRemove = 0x0001;

    /// <summary><c>XINPUT_CAPS_WIRELESS</c>.</summary>
    public const ushort XInputCapsWireless = 0x0002;

    /// <summary><c>XINPUT_FLAG_GAMEPAD</c>.</summary>
    public const uint XInputFlagGamepad = 0x00000001;

    /// <summary><c>BATTERY_DEVTYPE_GAMEPAD</c>.</summary>
    public const byte BatteryDevTypeGamepad = 0x00;

    /// <summary><c>HWND_MESSAGE</c>: the parent of a window that is never shown and receives no broadcast.</summary>
    public static readonly nint HwndMessage = -3;

    private const string _user32 = "user32.dll";
    private const string _kernel32 = "kernel32.dll";
    private const string _xinput = "xinput1_4.dll";

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true, SetLastError = true)]
    public static extern ushort RegisterClassExW(WndClassEx* windowClass);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true)]
    public static extern int UnregisterClassW(char* className, nint instance);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true, SetLastError = true)]
    public static extern nint CreateWindowExW(uint exStyle, char* className, char* windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, void* param);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true)]
    public static extern int DestroyWindow(nint window);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true)]
    public static extern nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true)]
    public static extern int PeekMessageW(Message* message, nint window, uint filterMin, uint filterMax, uint remove);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true)]
    public static extern nint DispatchMessageW(Message* message);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true, SetLastError = true)]
    public static extern int RegisterRawInputDevices(RawInputDevice* devices, uint count, uint size);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true, SetLastError = true)]
    public static extern uint GetRawInputBuffer(void* data, uint* size, uint headerSize);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true, SetLastError = true)]
    public static extern uint GetRawInputData(nint rawInput, uint command, void* data, uint* size, uint headerSize);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true, SetLastError = true)]
    public static extern uint GetRawInputDeviceInfoW(nint device, uint command, void* data, uint* size);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true, SetLastError = true)]
    public static extern uint GetRawInputDeviceList(RawInputDeviceListEntry* list, uint* count, uint size);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_user32, ExactSpelling = true)]
    public static extern int GetLastInputInfo(LastInputInfo* info);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_kernel32, ExactSpelling = true)]
    public static extern nint GetModuleHandleW(char* name);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_kernel32, ExactSpelling = true)]
    public static extern nint GetCurrentThread();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_kernel32, ExactSpelling = true)]
    public static extern int GetThreadTimes(nint thread, long* creation, long* exit, long* kernel, long* user);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_xinput, ExactSpelling = true)]
    public static extern uint XInputGetState(uint userIndex, XInputState* state);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_xinput, ExactSpelling = true)]
    public static extern uint XInputGetCapabilities(uint userIndex, uint flags, XInputCapabilities* capabilities);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_xinput, ExactSpelling = true)]
    public static extern uint XInputGetBatteryInformation(uint userIndex, byte deviceType, XInputBatteryInformation* battery);

    /// <summary><c>WNDCLASSEXW</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint> WndProc;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public char* MenuName;
        public char* ClassName;
        public nint SmallIcon;
    }

    /// <summary><c>MSG</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Message
    {
        public nint Window;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    /// <summary><c>RAWINPUTDEVICE</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint Target;
    }

    /// <summary><c>RAWINPUTDEVICELIST</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RawInputDeviceListEntry
    {
        public nint Device;
        public uint Type;
    }

    /// <summary><c>RID_DEVICE_INFO</c>, with the HID member of its union - the only one the reader looks at.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RidDeviceInfo
    {
        [FieldOffset(0)]
        public uint Size;

        [FieldOffset(4)]
        public uint Type;

        [FieldOffset(8)]
        public uint VendorId;

        [FieldOffset(12)]
        public uint ProductId;

        [FieldOffset(16)]
        public uint VersionNumber;

        [FieldOffset(20)]
        public ushort UsagePage;

        [FieldOffset(22)]
        public ushort Usage;
    }

    /// <summary><c>XINPUT_GAMEPAD</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLx;
        public short ThumbLy;
        public short ThumbRx;
        public short ThumbRy;
    }

    /// <summary><c>XINPUT_STATE</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    /// <summary><c>XINPUT_CAPABILITIES</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputCapabilities
    {
        public byte Type;
        public byte SubType;
        public ushort Flags;
        public XInputGamepad Gamepad;
        public ushort LeftMotor;
        public ushort RightMotor;
    }

    /// <summary><c>XINPUT_BATTERY_INFORMATION</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputBatteryInformation
    {
        public byte Type;
        public byte Level;
    }

    /// <summary><c>LASTINPUTINFO</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Input;

namespace FrameLedger.Application.Input;

/// <summary>
/// The input reader itself (<c>21_INPUT_DEVICES</c> §The reader): about ten times a second it takes what Windows has
/// to say - the controllers' reports that arrived, XInput's four pads, the time of the last keyboard or mouse input,
/// the window in front - and makes one <see cref="InputSlice"/> of it. It is the same for every session running; whose
/// game a slice counts for is <see cref="InputTickAccumulator"/>'s to decide.
/// </summary>
/// <remarks>
/// <para>
/// <b>What a controller is here.</b> Each of XInput's pads that is connected is one; each controller Windows does not
/// present through XInput is one. A device Windows presents through XInput is ALSO seen by raw input, and is not a
/// second controller: it gives the pads their name, when that cannot be mistaken, and is the witness of the next
/// paragraph.
/// </para>
/// <para>
/// <b>XInput in the background is not documented</b>: Microsoft's page for <c>XInputEnable</c> says that since
/// Windows 10 the system switches a program's controller input by its window's focus, and the Agent has no window in
/// front, ever. So the reader watches for it. A slice in which an XInput-class device's own reports changed while no
/// pad's packet number moved - not in this slice nor the one before - is counted
/// (<see cref="InputHealth.XInputSilent"/>). Once there are <see cref="SilentBeforeFallback"/> of them and XInput has
/// never once been seen to move, XInput is taken to be answering nothing here, and from then on such a slice counts
/// the lowest connected pad as in use: its reports changed, which a pad at rest does not do. That is coarser than
/// XInput's own dead zones - a drifting stick counts - and it is used only where the better source is silent.
/// </para>
/// <para>
/// <b>Which controllers are read for use.</b> XInput's pads, and a DualSense by its own reports
/// (<see cref="DualSenseReport"/>). Any other controller is listed, with its connections and drops, and is never "in
/// use": its report layout is not known to this build, and a guess would be a made-up number.
/// </para>
/// </remarks>
public sealed class InputReader : IInputBackendSink
{
    /// <summary>XInput's four pads.</summary>
    public const int Slots = 4;

    /// <summary>A pad that is not connected is asked about once in this many slices - XInput looks for a device each time it is asked about an empty one - and at once when a controller arrives or leaves.</summary>
    public const int ProbeEverySlices = 20;

    /// <summary>A connected pad's battery is asked for once in this many slices.</summary>
    public const int DetailsEverySlices = 50;

    /// <summary>How many silent slices, with XInput never seen to move, before a device's own reports stand in for it.</summary>
    public const int SilentBeforeFallback = 10;

    /// <summary>The number the first controller that is not one of XInput's gets; the pads are 1 to 4.</summary>
    private const int _firstRawId = 16;

    private readonly IInputBackend _backend;
    private readonly Dictionary<nint, Raw> _raw = [];
    private readonly Dictionary<(RawController Controller, int Instance), int> _rawIds = [];
    private readonly Pad[] _pads = [new(0), new(1), new(2), new(3)];
    private bool _registered;
    private bool _lastInputAvailable;
    private uint? _lastInputTick;
    private bool _probeNow = true;
    private bool _renamePads = true;
    private bool _movedLastSlice;
    private bool _xinputEverMoved;
    private long _slices;
    private long _rawReports;
    private long _rawReportsUnread;
    private long _xinputReads;
    private long _xinputSilent;
    private int _faults;

    public InputReader(IInputBackend backend) => _backend = backend ?? throw new ArgumentNullException(nameof(backend));

    /// <summary>
    /// The only device classes the reader registers raw input for: HID usage page 1 (generic desktop), usages 4
    /// (joystick) and 5 (gamepad). Never 2 (mouse) or 6 (keyboard): what is known of those is one time, from
    /// <see cref="IInputBackend.TryReadLastInputTick"/> (owner decision D158).
    /// </summary>
    public static IReadOnlyList<(ushort UsagePage, ushort Usage)> RegisteredUsages { get; } = [(1, 4), (1, 5)];

    /// <summary>Opens the backend on the calling thread, which is the one every <see cref="Slice"/> must then be called on.</summary>
    public void Open() => _registered = _backend.Open();

    /// <summary>A slice that threw is counted and skipped.</summary>
    public void Fault() => _faults++;

    /// <summary>What Windows has to say since the slice before, <paramref name="elapsedMs"/> ago.</summary>
    public InputSlice Slice(int elapsedMs)
    {
        _slices++;
        foreach (Raw raw in _raw.Values)
        {
            raw.Active = false;
            raw.Changed = false;
        }

        _backend.Drain(this);
        bool moved = ReadPads();
        WatchForSilence(moved);
        if (_renamePads)
        {
            NamePads();
        }

        bool keyboardMouse = false;
        if (_backend.TryReadLastInputTick(out uint tick))
        {
            _lastInputAvailable = true;
            keyboardMouse = _lastInputTick is { } before && tick != before;
            _lastInputTick = tick;
        }

        var devices = new List<InputSliceDevice>(Slots + _raw.Count);
        foreach (Pad pad in _pads)
        {
            if (pad.Connected)
            {
                devices.Add(new InputSliceDevice(pad.Device, pad.Active, pad.Details is { } d ? XInputPad.Battery(d.BatteryType, d.BatteryLevel) : null));
            }
        }

        foreach (Raw raw in _raw.Values.Where(static r => !r.Controller.XInputClass).OrderBy(static r => r.Device.Id))
        {
            devices.Add(new InputSliceDevice(raw.Device, raw.Active, raw.Battery));
        }

        return new InputSlice
        {
            ElapsedMs = Math.Max(0, elapsedMs),
            ForegroundProcessId = _backend.ForegroundProcessId(),
            KeyboardMouseActive = keyboardMouse,
            VirtualPad = _raw.Values.Any(static r => r.Controller is { XInputClass: true, IsVirtual: true }),
            Devices = devices,
            Health = new InputHealth
            {
                RawInputRegistered = _registered,
                RawReports = _rawReports,
                RawReportsUnread = _rawReportsUnread,
                XInputAvailable = _backend.XInputAvailable,
                XInputReads = _xinputReads,
                XInputSilent = _xinputSilent,
                LastInputAvailable = _lastInputAvailable,
                Slices = _slices,
                ThreadCpuMs = _backend.ThreadCpuMs(),
                Faults = _faults,
            },
        };
    }

    void IInputBackendSink.Arrived(nint device, RawController controller)
    {
        if (_raw.TryGetValue(device, out Raw? known) && known.Controller == controller)
        {
            // Windows names the controllers that are already there when the registration is made, and the reader has
            // asked for the list as well: the same device twice is one device.
            return;
        }

        int id = 0;
        if (!controller.XInputClass)
        {
            int instance = _raw.Values.Count(r => r.Controller == controller);
            if (!_rawIds.TryGetValue((controller, instance), out id))
            {
                id = _firstRawId + _rawIds.Count;
                _rawIds[(controller, instance)] = id;
            }
        }

        _raw[device] = new Raw(controller, new InputDevice
        {
            Id = id,
            Family = ControllerCatalog.FamilyOf(controller.VendorId, xinput: false),
            VendorId = controller.VendorId,
            ProductId = controller.ProductId,
            Connection = controller.Connection,
        });
        DevicesChanged(controller);
    }

    void IInputBackendSink.Removed(nint device)
    {
        if (_raw.Remove(device, out Raw? gone))
        {
            DevicesChanged(gone.Controller);
        }
    }

    void IInputBackendSink.Report(nint device, ReadOnlySpan<byte> report)
    {
        _rawReports++;
        if (!_raw.TryGetValue(device, out Raw? raw))
        {
            _rawReportsUnread++;
            return;
        }

        if (raw.Controller.XInputClass)
        {
            // The witness only: did the device say something new. What it said is XInput's to tell.
            raw.Changed |= raw.Last is not null && !report.SequenceEqual(raw.Last);
            if (raw.Last is null || raw.Last.Length != report.Length)
            {
                raw.Last = report.ToArray();
            }
            else
            {
                report.CopyTo(raw.Last);
            }
        }
        else if (raw.Controller.IsDualSense)
        {
            if (DualSenseReport.TryRead(report, raw.Controller.Connection == ControllerConnection.Bluetooth, out bool active, out BatteryReading? battery))
            {
                raw.Active |= active;
                raw.Battery = battery ?? raw.Battery;
            }
            else
            {
                _rawReportsUnread++;
            }
        }
    }

    private void DevicesChanged(RawController controller)
    {
        if (controller.XInputClass)
        {
            _probeNow = true;
            _renamePads = true;
        }
    }

    /// <summary>Asks XInput about its pads; returns whether any connected pad's packet number moved since the slice before.</summary>
    private bool ReadPads()
    {
        bool moved = false;
        bool probe = _probeNow || _slices % ProbeEverySlices == 1;
        _probeNow = false;
        foreach (Pad pad in _pads)
        {
            pad.Active = false;
            if (!_backend.XInputAvailable || (!pad.Connected && !probe))
            {
                continue;
            }

            if (!_backend.TryReadPad(pad.Slot, out XInputReading state))
            {
                if (pad.Connected)
                {
                    pad.Connected = false;
                    pad.Details = null;
                    _renamePads = true;
                }

                continue;
            }

            _xinputReads++;
            if (!pad.Connected)
            {
                pad.Connected = true;
                pad.Details = null;
                _renamePads = true;
            }
            else if (state.Packet != pad.Packet)
            {
                moved = true;
            }

            pad.Packet = state.Packet;
            pad.Active = XInputPad.IsActive(state.Buttons, state.LeftTrigger, state.RightTrigger, state.ThumbLx, state.ThumbLy, state.ThumbRx, state.ThumbRy);
            if (pad.Details is null || _slices - pad.DetailsAt >= DetailsEverySlices)
            {
                pad.DetailsAt = _slices;
                if (_backend.TryReadPadDetails(pad.Slot, out XInputDetails details))
                {
                    _renamePads |= pad.Details?.Wireless != details.Wireless;
                    pad.Details = details;
                }
            }
        }

        return moved;
    }

    private void WatchForSilence(bool moved)
    {
        _xinputEverMoved |= moved;
        bool deviceSpoke = _raw.Values.Any(static r => r.Controller.XInputClass && r.Changed);
        if (deviceSpoke && !moved && !_movedLastSlice && !_pads.Any(static p => p is { Connected: true, Active: true }))
        {
            _xinputSilent++;
            if (!_xinputEverMoved && _xinputSilent >= SilentBeforeFallback && _pads.FirstOrDefault(static p => p.Connected) is { } lowest)
            {
                lowest.Active = true;
            }
        }

        _movedLastSlice = moved;
    }

    /// <summary>
    /// What each connected pad is. XInput does not say which device a pad is, so a pad is named only when it cannot be
    /// another: every XInput-class device a player could hold has the same two ids, or the only ones there are
    /// Steam's. Otherwise it is "one of XInput's pads" - the Xbox family, no ids, no model.
    /// </summary>
    private void NamePads()
    {
        _renamePads = false;
        RawController[] presented = [.. _raw.Values.Where(static r => r.Controller.XInputClass).Select(static r => r.Controller)];
        RawController[] held = [.. presented.Where(static c => !c.IsVirtual)];
        RawController? name = held.Length > 0
            ? held.All(c => c.VendorId == held[0].VendorId && c.ProductId == held[0].ProductId) ? held[0] : null
            : presented.Length > 0 ? presented[0] : null;
        bool bluetooth = name is not null && held.Length > 0 && held.All(static c => c.Connection == ControllerConnection.Bluetooth);
        foreach (Pad pad in _pads)
        {
            pad.Device = new InputDevice
            {
                Id = pad.Slot + 1,
                XInputSlot = pad.Slot,
                Family = ControllerCatalog.FamilyOf(name?.VendorId ?? 0, xinput: true),
                VendorId = name?.VendorId ?? 0,
                ProductId = name?.ProductId ?? 0,
                IsVirtual = name?.IsVirtual ?? false,
                Connection = bluetooth
                    ? ControllerConnection.Bluetooth
                    : pad.Details switch
                    {
                        { Wireless: true } => ControllerConnection.Wireless,
                        { } => ControllerConnection.Usb,
                        _ => ControllerConnection.Unknown,
                    },
            };
        }
    }

    /// <summary>One of XInput's pads.</summary>
    private sealed class Pad(int slot)
    {
        public int Slot { get; } = slot;

        public bool Connected { get; set; }

        public uint Packet { get; set; }

        public bool Active { get; set; }

        public XInputDetails? Details { get; set; }

        public long DetailsAt { get; set; }

        public InputDevice Device { get; set; } = new() { Id = slot + 1, XInputSlot = slot, Family = ControllerFamily.Xbox };
    }

    /// <summary>A controller raw input named.</summary>
    private sealed class Raw(RawController controller, InputDevice device)
    {
        public RawController Controller { get; } = controller;

        public InputDevice Device { get; } = device;

        public bool Active { get; set; }

        public bool Changed { get; set; }

        public BatteryReading? Battery { get; set; }

        public byte[]? Last { get; set; }
    }
}

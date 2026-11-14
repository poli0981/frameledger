// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Input;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Infrastructure.Blobs;

namespace FrameLedger.App.Tests;

/// <summary>
/// Eight seconds of a session played with two controllers (0.1.0-beta.50), written as the recorder writes them: the
/// row's totals, <c>sessions.input_devices</c> and the <c>input.*</c> series. Synthetic - no controller was read to
/// make it, which is what every test over it can and cannot show.
/// </summary>
/// <remarks>
/// Second by second: a controller, a controller, both, keyboard and mouse twice, no input, the game not in front, a
/// controller. Controller 1 (a DualSense over Bluetooth) is there throughout and its battery falls from 40 % to 10 %
/// at the sixth second. Controller 2 (an Xbox pad on a wireless adapter) connects at the third second, drops at the
/// fifth, comes back at the seventh with its battery fallen from medium to low.
/// </remarks>
internal static class InputFixtures
{
    private const float N = float.NaN;

    public static SensorBlob[] Blobs() =>
    [
        Blob("t_ms", SeriesCodec.EncodeFloat32([0f, 1000f, 2000f, 3000f, 4000f, 5000f, 6000f, 7000f])),
        Blob(InputSessionBuilder.ClassSeries, SeriesCodec.EncodeSparseFloat32([1f, 1f, 3f, 2f, 2f, 0f, N, 1f])),
        Blob(InputSessionBuilder.ActiveSeries(1), SeriesCodec.EncodeSparseFloat32([1f, 1f, 1f, 0f, 0f, 0f, 0f, 1f])),
        Blob(InputSessionBuilder.BatterySeries(1), SeriesCodec.EncodeSparseFloat32([40f, 40f, 40f, 30f, 20f, 10f, 10f, 10f])),
        Blob(InputSessionBuilder.ActiveSeries(2), SeriesCodec.EncodeSparseFloat32([N, N, 0.5f, 0f, N, N, 0f, 0f])),
        Blob(InputSessionBuilder.BatterySeries(2), SeriesCodec.EncodeSparseFloat32([N, N, 2f, 2f, N, N, 1f, 1f])),
    ];

    /// <summary>The same with eight ticks of <c>gpu_temp</c>, for a session that has frames to draw them over.</summary>
    public static SensorBlob[] BlobsWithSensors() =>
        [.. Blobs(), Blob("gpu_temp", SeriesCodec.EncodeFloat32([60f, 61f, 62f, 63f, 64f, 65f, 66f, 67f]))];

    public static InputDevicesRecord Record() => new()
    {
        Devices =
        [
            new InputDeviceRecord
            {
                Index = 1,
                Family = "playstation",
                VendorId = 0x054C,
                ProductId = 0x0CE6,
                Model = "DualSense Wireless Controller",
                Connection = "bluetooth",
                ConnectedMs = 8000,
                ActiveMs = 4000,
                Battery = new InputBatteryRecord
                {
                    Scale = "percent",
                    States = [new InputBatteryStateRecord("discharging", 40, 3000), new InputBatteryStateRecord("discharging", 10, 3000)],
                    First = 40,
                    Last = 10,
                    DrainPercentPerHour = 13.5,
                },
            },
            new InputDeviceRecord
            {
                Index = 2,
                Family = "xbox",
                VendorId = 0x045E,
                ProductId = 0x0B12,
                Model = "Xbox Wireless Controller",
                Connection = "wireless",
                XInputSlot = 0,
                ConnectedMs = 4000,
                ActiveMs = 500,
                Connects = 2,
                Disconnects = 1,
                Battery = new InputBatteryRecord
                {
                    Scale = "levels",
                    States = [new InputBatteryStateRecord("discharging", 2, 2000), new InputBatteryStateRecord("discharging", 1, 2000)],
                    First = 2,
                    Last = 1,
                },
            },
        ],
        Families = [new InputFamilyRecord("playstation", 4000, 0.8), new InputFamilyRecord("xbox", 1000, 0.2)],
        FamilySwitches = 2,
        Health = new InputHealth { RawInputRegistered = true, XInputAvailable = true, LastInputAvailable = true, RawReports = 480, XInputReads = 60, Slices = 80 },
    };

    /// <summary>A row with the fixture's totals: read for 8 s, in front for 7 s - 3 s a controller, 2 s keyboard and mouse, 1 s both, 1 s nothing.</summary>
    public static SessionRow With(SessionRow row) => row with
    {
        InputObservedMs = 8000,
        InputForegroundMs = 7000,
        InputControllerMs = 3000,
        InputKeyboardMouseMs = 2000,
        InputBothMs = 1000,
        InputIdleMs = 1000,
        InputMethodSwitches = 2,
        InputSource = "rawinput+xinput+lastinput",
        InputDevicesJson = InputDevicesRecord.ToJson(Record()),
    };

    private static SensorBlob Blob(string series, byte[] data) => new() { Series = series, Hz = 1, Codec = SeriesCodec.Tag, Data = data };
}

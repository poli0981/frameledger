// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>How a controller reached the PC, as far as Windows says.</summary>
public enum ControllerConnection
{
    /// <summary>Nothing said how.</summary>
    Unknown = 0,

    /// <summary>A cable.</summary>
    Usb,

    /// <summary>Bluetooth, classic or low energy.</summary>
    Bluetooth,

    /// <summary>A wireless link that is not known to be Bluetooth - XInput's own word, with no more detail.</summary>
    Wireless,
}

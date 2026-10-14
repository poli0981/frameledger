// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Media;

namespace FrameLedger.App.Services;

/// <summary>
/// The game's image for the edit dialog (beta.23, D60): what the page shows now and whether it is the user's own, the store's
/// own cover for "Use the store's image", the picker, and how a picked file is previewed.
/// </summary>
public sealed record EditImage(ImageSource? Current, bool CurrentIsOwn, ImageSource? Store, IImagePicker Picker, Func<string, Task<ImageSource?>> Preview);

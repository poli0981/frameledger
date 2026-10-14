// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Microsoft.Win32;

namespace FrameLedger.App.Services;

/// <summary>The WPF <see cref="OpenFileDialog"/> for a game's image (beta.23, D60): the formats Windows' own codecs read.</summary>
public sealed class ImagePicker : IImagePicker
{
    public string? PickImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.ImagePicker_Title,
            Filter = Strings.ImagePicker_Filter,
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.23 (owner decision D60): the edit dialog's image row. A picked image is previewed and copied only on Save; going
/// back to the store's image drops the row's own; a file that is no image is said, and nothing is kept of it.
/// </summary>
public sealed class EditGameViewModelTests
{
    private static readonly ImageSource _store = Frozen();
    private static readonly ImageSource _own = Frozen();
    private static readonly ImageSource _picked = Frozen();

    private static BitmapSource Frozen()
    {
        BitmapSource image = BitmapSource.Create(2, 3, 96, 96, PixelFormats.Bgra32, null, new byte[24], 8);
        image.Freeze();
        return image;
    }

    private sealed class Picker(string? path) : IImagePicker
    {
        public int Asked { get; private set; }

        public string? PickImage()
        {
            Asked++;
            return path;
        }
    }

    private static EditImage Image(Picker picker, bool own, bool readable = true) =>
        new(own ? _own : _store, own, _store, picker, _ => Task.FromResult(readable ? _picked : null));

    [Fact]
    public void WithoutAnImageRowTheDialogIsAsItWas()
    {
        var vm = new EditGameViewModel(new GameMetadata { Name = "Alpha", CoverPath = "covers\\game-1-2.png" }, provenanceJson: null);

        vm.ImageEditable.Should().BeFalse();
        GameEdit edit = vm.Edit();
        edit.PickedImage.Should().BeNull();
        edit.Metadata.CoverPath.Should().Be("covers\\game-1-2.png", "an edit that did not touch the image keeps the row's");
    }

    [Fact]
    public async Task APickedImageIsPreviewedAndHandedOnOnlyWithTheSave()
    {
        var picker = new Picker(@"C:\Pictures\alpha.png");
        var vm = new EditGameViewModel(new GameMetadata { Name = "Alpha" }, null, Image(picker, own: false));
        vm.ImagePreview.Should().BeSameAs(_store);
        vm.CanUseStoreImage.Should().BeFalse("the store's image is the one shown");

        await vm.ChooseImageCommand.ExecuteAsync(null);

        vm.ImagePreview.Should().BeSameAs(_picked);
        vm.PickedImage.Should().Be(@"C:\Pictures\alpha.png");
        vm.CanUseStoreImage.Should().BeTrue();
        vm.Edit().PickedImage.Should().Be(@"C:\Pictures\alpha.png", "Save hands the file on to be copied");

        vm.UseStoreImageCommand.Execute(null);
        vm.ImagePreview.Should().BeSameAs(_store);
        vm.Edit().PickedImage.Should().BeNull("going back to the store's image drops the pick");
    }

    [Fact]
    public async Task AFileThatIsNoImageIsSaidAndNotKept()
    {
        var vm = new EditGameViewModel(new GameMetadata { Name = "Alpha" }, null, Image(new Picker(@"C:\notes.txt"), own: false, readable: false));

        await vm.ChooseImageCommand.ExecuteAsync(null);

        vm.ImageError.Should().Be(Strings.Art_ImportFailed);
        vm.PickedImage.Should().BeNull();
        vm.ImagePreview.Should().BeSameAs(_store);
    }

    [Fact]
    public async Task UsingTheStoresImageDropsTheRowsOwnAndACancelledPickChangesNothing()
    {
        var picker = new Picker(null);
        var vm = new EditGameViewModel(new GameMetadata { Name = "Alpha", CoverPath = "covers\\game-1-2.png" }, null, Image(picker, own: true));
        vm.CanUseStoreImage.Should().BeTrue("the user's own image is the one shown");

        await vm.ChooseImageCommand.ExecuteAsync(null);
        picker.Asked.Should().Be(1);
        vm.ImagePreview.Should().BeSameAs(_own, "a cancelled pick changes nothing");

        vm.UseStoreImageCommand.Execute(null);
        vm.Edit().Metadata.CoverPath.Should().BeNull("Save drops the row's own image");
        vm.ImagePreview.Should().BeSameAs(_store);
        vm.CanUseStoreImage.Should().BeFalse();
    }
}

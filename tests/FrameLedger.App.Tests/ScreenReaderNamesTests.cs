// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Controls;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.App.Tests;

/// <summary>
/// The words a screen reader is given (beta.35, D88), one piece at a time; <c>PagesLoadTests.Automation</c> sweeps every
/// surface for the same faults. English, with the thread's number format pinned, since the words are the translations'.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class ScreenReaderNamesTests
{
    private static T InEnglish<T>(Func<T> read)
    {
        CultureInfo strings = Strings.Culture ?? CultureInfo.CurrentUICulture;
        CultureInfo thread = CultureInfo.CurrentCulture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            return read();
        }
        finally
        {
            Strings.Culture = strings;
            CultureInfo.CurrentCulture = thread;
        }
    }

    [Fact]
    public async Task ADropDownsOptionIsReadByItsLabelNotItsRecord()
    {
        var option = new Choice<string>("dark", "Dark");
        string name = await PagesLoadTests.OnStaAsync(() => UIElementAutomationPeer.CreatePeerForElement(new ComboBoxItem { Content = option })!.GetName());

        option.ToString().Should().Be("Dark");
        name.Should().Be("Dark", "WPF names a drop-down's item by the item's ToString(); the record's own was 'Choice { Value = dark, Label = Dark }'");
    }

    /// <summary>CLAUDE.md rule 6 in speech: Native, Displayed and the factor together; a steady factor with its share.</summary>
    [Fact]
    public void TheReadoutSaysNativeDisplayedAndTheFactorTogether()
    {
        (string Generated, string Steady, string Presented, string None, string Unavailable, string Qualifier, string Fps62, string Fps118) said = InEnglish(() => (
            new FpsReadoutModel { Kind = FpsReadoutKind.Generated, Native = 62, Displayed = 118, Factor = 1.9 }.SpokenText,
            new FpsReadoutModel { Kind = FpsReadoutKind.Generated, Native = 62, Displayed = 118, Factor = 1.9, SteadyShare = 0.75 }.SpokenText,
            new FpsReadoutModel { Kind = FpsReadoutKind.Presented, Presented = 62, Qualifier = FpsQualifier.NoRuntime }.SpokenText,
            new FpsReadoutModel { Kind = FpsReadoutKind.None, Native = 144 }.SpokenText,
            FpsReadoutModel.Unavailable.SpokenText,
            FpsPresentation.QualifierText(FpsQualifier.NoRuntime),
            Formats.Fps(62),
            Formats.Fps(118)));

        said.Generated.Should().Be($"Native {said.Fps62} FPS, displayed {said.Fps118} FPS, frame generation ×1.9");
        said.Steady.Should().Be($"Native {said.Fps62} FPS, displayed {said.Fps118} FPS, frame generation ×1.9 for 75% of the session");
        said.Presented.Should().Be($"{said.Fps62} presented FPS. {said.Qualifier}", "a stand-alone number is never said without its qualifier");
        said.None.Should().EndWith("FPS").And.NotContain("×");
        said.Unavailable.Should().Be("N/A");
    }

    [Fact]
    public void EveryItemIsReadAsItShows()
    {
        var said = InEnglish(() => new
        {
            Chip = new TriStateChipModel("Ray Tracing", Tri.Yes, TriStateSource.Measured).ToString(),
            Notice = new SafetyNotice(SafetyNoticeKind.Refused, "Alpha: hooking refused", "Easy Anti-Cheat was found.", DateTimeOffset.UnixEpoch).ToString(),
            Detail = new GameDetailRow("Runs as", "Direct3D 12").ToString(),
            Fact = new LabelledValue("GPU", "RTX 5080").ToString(),
            Card = new StatCardModel("Average", "62", "FPS").ToString(),
            Best = new CompareCell("62", IsBest: true).ToString(),
            Row = new CompareRowViewModel("Average", [new CompareCell("62", true), new CompareCell("58", false)]).ToString(),
            Change = new ChangeMarker(2, DateTimeOffset.UnixEpoch, ["GPU driver 576.02"]).ToString(),
            Sensor = new SensorStatRowModel("GPU temperature", "60 °C", "61 °C", "40 °C", "70 °C", "120").ToString(),
            Page = new DocumentPage("Accessibility", "# Accessibility\n\nA long text.", "ACCESSIBILITY.md").ToString(),
        });

        said.Chip.Should().Be("Ray Tracing: Yes, measured", "the source is the tooltip's to the eye and the chip takes no focus");
        said.Notice.Should().Be("Alpha: hooking refused. Easy Anti-Cheat was found.");
        said.Detail.Should().Be("Runs as: Direct3D 12");
        said.Fact.Should().Be("GPU: RTX 5080");
        said.Card.Should().Be("Average: 62 FPS");
        said.Best.Should().Be("62, the best", "bold says it to the eye");
        said.Row.Should().Be("Average: 62, the best, 58");
        said.Change.Should().StartWith("Change 2, ").And.EndWith(": GPU driver 576.02");
        said.Sensor.Should().Be("GPU temperature: mean 60 °C, median 61 °C, lowest 40 °C, highest 70 °C, over 120 readings");
        said.Page.Should().Be("Accessibility", "a page list's item is its title, never the page's whole text");
    }

    /// <summary>A control that only shows something speaks through its own peer: a Text element, its words, no children.</summary>
    [Fact]
    public async Task TheControlsThatOnlyShowSpeakThroughTheirPeers()
    {
        var model = new FpsReadoutModel { Kind = FpsReadoutKind.Presented, Presented = 62, Qualifier = FpsQualifier.RuntimeLoaded };
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var readout = new FpsReadout { Model = model };
            AutomationPeer readoutPeer = UIElementAutomationPeer.CreatePeerForElement(readout)!;
            var chip = new TriStateChip { Model = new TriStateChipModel("RT", Tri.No, TriStateSource.Manual) };
            var badge = new ChangeBadge { Text = "3" };
            var named = new FpsReadout { Model = model };
            AutomationProperties.SetName(named, "set by hand");
            var styled = new FpsReadout { Model = model };
            var host = new Wpf.Ui.Controls.FluentWindow { Content = styled };
            Visuals.Layout(styled, 400, 100);
            host.Close();
            return new
            {
                Type = readoutPeer.GetAutomationControlType(),
                Name = readoutPeer.GetName(),
                Help = readoutPeer.GetHelpText(),
                Children = readoutPeer.GetChildren(),
                Class = readoutPeer.GetClassName(),
                Chip = UIElementAutomationPeer.CreatePeerForElement(chip)!.GetName(),
                ChipText = chip.Model!.AccessibleName,
                Badge = UIElementAutomationPeer.CreatePeerForElement(badge)!.GetName(),
                Own = UIElementAutomationPeer.CreatePeerForElement(named)!.GetName(),
                styled.Focusable,
            };
        });

        facts.Type.Should().Be(AutomationControlType.Text);
        facts.Name.Should().Be(model.SpokenText);
        facts.Help.Should().Be(model.QualifierTooltip, "the tooltip's words, which a chip that takes no focus never shows the keyboard");
        facts.Children.Should().BeNull("the template's pieces are not read a second time");
        facts.Class.Should().Be(nameof(FpsReadout));
        facts.Chip.Should().Be(facts.ChipText);
        facts.Badge.Should().EndWith("3");
        facts.Own.Should().Be("set by hand", "a name the XAML gives wins over the control's own words");
        facts.Focusable.Should().BeFalse("shown, not operated: the App's style takes it out of the tab order");
    }

    [Fact]
    public async Task ATemplateColumnsCellsAreNamedByTheirRowsTextOverWpfUisCellStyle()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var column = new DataGridTemplateColumn();
            CellName.SetPath(column, "NativeText");
            Style style = column.CellStyle;
            var setter = (Setter)style.Setters.Single();
            return new
            {
                style.TargetType,
                BasedOnWpfUi = ReferenceEquals(style.BasedOn, System.Windows.Application.Current.TryFindResource(CellName.WpfUiCellStyleKey)),
                setter.Property,
                Path = ((Binding)setter.Value).Path.Path,
                Again = CellName.Named("X", basedOn: null).BasedOn,
            };
        });

        facts.TargetType.Should().Be<DataGridCell>();
        facts.BasedOnWpfUi.Should().BeTrue("the cells keep WPF UI's look: its DataGrid gives every cell DefaultDataGridCellStyle");
        facts.Property.Should().Be(AutomationProperties.NameProperty);
        facts.Path.Should().Be("NativeText");
        facts.Again.Should().BeNull();
        InEnglish(() => (CellName.Spoken(null), CellName.Spoken("  "), CellName.Spoken("62"))).Should().Be(("empty", "empty", "62"),
            "a cell with no text is said to be empty, never WPF's 'Item: …, Column Display Index: n'");
    }

    [Fact]
    public async Task AnExpandersButtonIsNamedAfterItsHeader()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var composed = new StackPanel { Children = { new TextBlock { Text = "Kill switch" }, new TextBlock { Text = "Off" } } };
            var expander = new Wpf.Ui.Controls.CardExpander { Header = "Details", Content = new TextBlock { Text = "inside" } };
            var host = new Wpf.Ui.Controls.FluentWindow { Content = expander };
            try
            {
                Visuals.Layout(expander, 600, 300);
                Visuals.Pump();
                var toggle = (ToggleButton?)expander.Template?.FindName(ExpanderToggleName.PartName, expander);
                return new
                {
                    Text = ExpanderToggleName.HeaderText("Details"),
                    Composed = ExpanderToggleName.HeaderText(composed),
                    Nothing = ExpanderToggleName.HeaderText(null),
                    Styled = ExpanderToggleName.GetIsEnabled(expander),
                    Toggle = toggle is null ? null : AutomationProperties.GetName(toggle),
                };
            }
            finally
            {
                host.Close();
            }
        });

        facts.Text.Should().Be("Details");
        facts.Composed.Should().Be("Kill switch", "a composed header is named by the first text it shows");
        facts.Nothing.Should().BeNull();
        facts.Styled.Should().BeTrue("the App's implicit CardExpander style turns the naming on for every expander");
        facts.Toggle.Should().EndWith("Details", "WPF UI 4.3.0's expand button has no name of its own (beta.35)");
    }
}

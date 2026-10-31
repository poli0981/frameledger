// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Shared;

namespace FrameLedger.Agent.Tests;

/// <summary>
/// D74 (2026-10-31): an alpha build's <c>--serve</c> waits for the Alpha notice at the version this build carries; the main
/// line serves whatever the table holds.
/// </summary>
public sealed class AlphaNoticeGateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheMainLineServesWithNoRowAtAll()
    {
        (await AlphaNoticeGate.AllowsServingAsync(BuildLine.Main, new Store(), Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task AnAlphaWaitsUntilTheNoticeIsAcceptedAtThisBuildsVersion()
    {
        var store = new Store();
        (await AlphaNoticeGate.AllowsServingAsync(BuildLine.Alpha, store, Ct)).Should().BeFalse("nothing accepted yet");

        await store.RecordAsync(new LegalAcceptance(AlphaNotice.DocumentKey, "0.9", DateTimeOffset.UnixEpoch), Ct);
        (await AlphaNoticeGate.AllowsServingAsync(BuildLine.Alpha, store, Ct)).Should().BeFalse("an older notice is not this one");

        await store.RecordAsync(new LegalAcceptance(AlphaNotice.DocumentKey, AlphaNotice.Version, DateTimeOffset.UnixEpoch), Ct);
        (await AlphaNoticeGate.AllowsServingAsync(BuildLine.Alpha, store, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task OtherDocumentsDoNotStandInForTheNotice()
    {
        var store = new Store();
        await store.RecordAsync(new LegalAcceptance("eula", AlphaNotice.Version, DateTimeOffset.UnixEpoch), Ct);

        (await AlphaNoticeGate.AllowsServingAsync(BuildLine.Alpha, store, Ct)).Should().BeFalse();
    }

    private sealed class Store : ILegalAcceptanceStore
    {
        private readonly Dictionary<string, LegalAcceptance> _rows = new(StringComparer.Ordinal);

        public ValueTask<LegalAcceptance?> FindAsync(string document, CancellationToken ct = default) =>
            ValueTask.FromResult(_rows.GetValueOrDefault(document));

        public ValueTask<IReadOnlyList<LegalAcceptance>> ListAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<LegalAcceptance>>([.. _rows.Values]);

        public ValueTask RecordAsync(LegalAcceptance acceptance, CancellationToken ct = default)
        {
            _rows[acceptance.Document] = acceptance;
            return ValueTask.CompletedTask;
        }
    }
}

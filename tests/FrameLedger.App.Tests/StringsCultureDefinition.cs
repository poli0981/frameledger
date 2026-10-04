// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Tests;

/// <summary>
/// Since beta.13 <see cref="StringsCultureCollection"/> runs ALONE (<see cref="CollectionDefinitionAttribute.DisableParallelization"/>:
/// after every parallel collection, and beside none). A collection only serializes its own members; every other class still
/// ran next to them, and a class that READS resource or FPS text read the wrong language while one of these had flipped the
/// culture — <c>GamesViewModelTests</c> on 2026-10-03, beside <c>GameMemoryTextTests</c>' many flips. Moving each reader in
/// found the races one red run at a time; taking the writers out of the parallel phase ends them as a class.
/// <see cref="StringsTests"/> proves the definition is the one bound.
/// </summary>
[CollectionDefinition(StringsCultureCollection.Name, DisableParallelization = true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires a collection definition to be public (xUnit1027)")]
public sealed class StringsCultureDefinition;

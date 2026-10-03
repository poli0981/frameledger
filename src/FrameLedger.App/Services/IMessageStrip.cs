// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The 4 s transient notice (<c>08_UI</c> §Notifications policy, "in-app, transient"), behind an interface so view models are testable. Safety events never go here.</summary>
public interface IMessageStrip
{
    void Info(string title, string body);

    void Success(string title, string body);

    void Warn(string title, string body);
}

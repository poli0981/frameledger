# FrameLedger — End User License Agreement (EULA)

**Version:** 1.3 · **Effective:** {{RELEASE_DATE}}

This End User License Agreement ("Agreement") applies to **FrameLedger** ("the Software"), developed and published by **poli0981** ("the Developer"), contactable at <contact@poli0981.dev> — see <https://poli0981.dev/> for other contact channels.

## 1. License

The Software is free and open-source software licensed to you under the **GNU General Public License, version 3.0 only (GPL-3.0-only)**. The full license text is provided in the `LICENSE` file and at <https://www.gnu.org/licenses/gpl-3.0.html>. Nothing in this Agreement limits or modifies your rights under the GPL-3.0. In case of conflict between this Agreement and the GPL-3.0, the GPL-3.0 prevails for everything it covers.

## 2. What the Software does

The Software records game performance data, hardware telemetry (temperatures, load, memory usage), and game metadata, and stores this data **locally on your device**. Details are described in the Privacy Policy and Disclaimer accompanying the Software.

## 2A. Code injection — your responsibility

To measure rendering settings accurately, the Software can load a component into a game process, **but only for games you have individually enabled**, after a consent prompt that explains the risk. The Software refuses to do so when it detects anti-cheat or anti-tamper software, turns hooking off for that game, and provides no means to override that refusal — except a narrow, per-game exception for anti-cheat that works entirely in user mode, which you may make yourself, after a separate disclosure, for a game the Software has already measured successfully, and under which the Software still runs every check (Disclaimer §2A). By making such an exception you accept, for that game, the risk described in (b) below.

By enabling this feature for a game, you confirm that:

(a) you are responsible for complying with the terms of service of that game and its platform;

(b) you understand that anti-cheat systems may detect the Software and may warn, block, or **permanently ban** your account, and that this risk is yours alone;

(c) you understand that the Software's protective checks cannot cover every anti-cheat system and cannot guarantee your account's safety;

(d) the Software is intended for **offline and single-player play**, and any use with an online or competitive title is entirely at your own risk;

(e) the Developer has no ability to reverse a ban, recover lost progress, or intervene with any game publisher on your behalf, and accepts no liability for such outcomes.

The Software records without injecting unless you enable injection for a specific game. In that mode it records only the session's duration and whatever hardware sensor data is available, indicates that mode on the session, and reports unavailable measurements as such rather than estimating them. Neither mode requires the Software's agent to run with administrator rights.

## 2B. Pre-release software, and your PC

The Software is currently distributed only as **pre-release (beta) builds**, which certainly still contain defects
(Disclaimer §0). You use a pre-release build at your own risk, and the Developer is not responsible for any incident
that results.

You are responsible for making sure your PC meets at least the minimum system requirements published by the developer
or publisher of each game you measure. Problems that arise from running a game below its minimum requirements, or on
unstable, overheating, overclocked or faulty hardware, are your responsibility and not the Developer's (Disclaimer
§4A).

## 3. Acceptance

By clicking "Accept" in the first-run dialog or by using the Software, you confirm that you have read this Agreement, the Disclaimer, and the Privacy Policy.

## 4. Third-party components

The Software includes and interoperates with third-party components listed in `legal/THIRD_PARTY_NOTICES.md`, each under its own license. The optional **PawnIO** kernel driver is a separate third-party product installed by you at your discretion and governed by its own license and terms.

## 5. No warranty

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT, TO THE MAXIMUM EXTENT PERMITTED BY APPLICABLE LAW AND AS STATED IN SECTIONS 15–17 OF THE GPL-3.0.

## 6. Limitation of liability

To the maximum extent permitted by applicable law, and as stated in the GPL-3.0, the Developer shall not be liable for any damages arising from the use or inability to use the Software, including but not limited to: data loss, hardware issues, interference with other software (including anti-cheat systems), or decisions made based on the measurements the Software reports.

## 7. Your responsibilities

You are responsible for: (a) complying with the terms of service of games and platforms you use alongside the Software; (b) deciding whether to install optional components such as PawnIO; (c) reviewing any bug report contents before submitting them.

## 8. Updates

The Software can check for updates via GitHub. Installing updates is always your choice. Updated legal documents will be presented for re-acceptance when their version changes.

## 9. Termination

Your rights under the GPL-3.0 continue as described in that license. You may stop using the Software at any time by uninstalling it; local data removal options are offered during uninstall.

---

Contact: <contact@poli0981.dev> · Developer: <https://poli0981.dev/> · Project: <https://github.com/poli0981/frameledger>

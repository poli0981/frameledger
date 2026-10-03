# Security policy

FrameLedger loads a component into game processes that the user has individually enabled, and it
refuses to do so when it detects anti-cheat or anti-tamper software. Two kinds of report matter here,
and they take different routes.

## A vulnerability in FrameLedger

Anything that could let FrameLedger be used against the person running it or against a third party:
a way to make the Agent inject into a process the user did not enable, a way past the anti-cheat
guard, a way to make the hook read or write game memory it does not own, a way to make the App or
Agent execute something from an untrusted source, a privacy leak (data leaving the machine, or the
bug bundle carrying what the privacy policy says it does not).

**Report it privately.** Use GitHub's private vulnerability reporting on this repository when it is
enabled (Security ▸ Report a vulnerability); otherwise e-mail <contact@poli0981.dev>. Please do not
open a public issue for a vulnerability until a fix has shipped.

What helps: the version (Help ▸ About, or `FrameLedger.exe --diag`), the steps, and what you observed.
What to leave out: instructions for defeating any anti-cheat system — FrameLedger needs to know what
to refuse, never how to evade (`docs/19_SAFETY_AND_ANTICHEAT.md`).

This is a one-person project. The intent is to acknowledge a private report within a week and to say
plainly what will happen next; that is an intent, not a contractual response time.

## The guard has no override

For two pre-releases (0.1.0-beta.3 and beta.4) a user could overrule the anti-cheat guard for one game through a
disclosure. That switch was withdrawn on 2026-09-22 (`docs/19_SAFETY_AND_ANTICHEAT.md` §What a finding does to the game): a finding
now turns the game's hooking off. What IS a security report: any way to reach an injection past a refusal — a flag, a
setting, a crafted rules file, a pipe message, a database edit the Agent honours — or a finding about a game that
does not turn its hooking off.

Since 0.1.0-beta.9 one narrow exception exists (owner decision D33, `docs/19_SAFETY_AND_ANTICHEAT.md` §The user-mode
exception): per game, only for one anti-cheat family that is user-mode throughout, with the guard still running every
check. The grant is a record in your own database, like the consent stamp; what keeps it narrow is the guard, which
decides itself which family it may let through. So these are security reports too: the guard letting through a family
that has a driver, a service or a `.sys` in the floor or the rules; an exception applying to a game with a kernel
driver in its folder, on a title list, or with a second anti-cheat; the Agent writing a grant on a pipe message
without its own tolerant pre-scan; an exception that survives a new finding, a crash or safety stop under it, or a
changed executable; since 0.1.0-beta.11 (D38), an exception that survives a session during its two-session trial that
did not go well, and any grant written for a game whose earlier exception failed its trial; and the Overlay tolerating
a family the guard did not.

## Running the agent as administrator

Since 0.1.0-beta.10 the capture agent can run as administrator — an option, off by default (owner decision D34,
`docs/01_ARCHITECTURE.md` ADR-9) — and Windows asks at every start of the agent. The residual risk is stated to the user
rather than solved: FrameLedger installs per user and unsigned, so a program running under the same account could replace
its files and be run as administrator when the user next accepts the prompt. What IS a security report: any way the
agent becomes administrator WITHOUT that prompt (a task, a service, an inherited token the option did not choose); a game
or any other program that an elevated agent starts with its own token instead of the desktop shell's; any privilege the
agent enables (none is — `tools/chokepoint-check.ps1` forbids the enablers under `src/`); an elevated agent that runs for
another account than the one that asked; and a named object an elevated agent creates that the same user's unelevated
processes can no longer open, or that another user can.

## A safety gap — a game with anti-cheat that FrameLedger fails to detect

This is treated with the same priority as a security report, and it takes the **public** route: open
an issue with the *Safety gap* form (`.github/ISSUE_TEMPLATE/safety_gap.yml`). Blocklist entries are
data (`rules/detection-rules.json`), so the fix is small — but the software has no rules-update path
yet, so a fix reaches installed copies only with the next release (`README` §Reporting a safety gap
says the same, and `docs/20_OPEN_QUESTIONS.md` §S20 carries the feed that would change it).

## Scope notes

- Releases are not code-signed; `SHA256SUMS.txt` is published beside every asset (`docs/11_UPDATER.md`
  §Unsigned releases). A report that an installer's hash does not match what the release lists is a
  security report.
- The evasion techniques FrameLedger will never implement are listed in `docs/19_SAFETY_AND_ANTICHEAT.md`.
  A pull request that makes FrameLedger harder for anti-cheat to see is declined on principle, not
  reviewed for quality.

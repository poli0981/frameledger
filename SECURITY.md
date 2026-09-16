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

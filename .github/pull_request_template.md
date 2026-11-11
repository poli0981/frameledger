<!--
Definition of done is CLAUDE.md §Definition of done. This template is that
list, so it is checked rather than remembered.
-->

## What and why

<!-- What changed, and which doc or issue drove it. -->

## Definition of done

- [ ] Builds warning-free (C# **and** C++); tests green
- [ ] `dotnet format` and `clang-format` clean (`./build.ps1 check`)
- [ ] Any deviation from a doc updates that doc **in this PR**
- [ ] New user-visible strings exist in `en`, `vi` and `ja`

## Capture-layer changes only

Skip this section if the PR does not touch the Overlay, the injector, the
Vulkan layer, or the guard. Otherwise every box must be ticked — these are the
review questions from `docs/19_SAFETY_AND_ANTICHEAT.md` §Review checklist.

- [ ] No hook reads anything beyond the arguments of the API it hooks (CLAUDE.md rule 4)
- [ ] Nothing here makes FrameLedger harder for anti-cheat to identify (rule 3)
- [ ] No new path can reach `Injector.Attach` without a passing guard result (rule 2)
- [ ] Every new hook is listed in `docs/17_HOOK_ENGINE.md` §Hook inventory with a stated purpose
- [ ] No hook body allocates, locks, logs, or throws (rule 5)
- [ ] When this hook is unavailable the session records `N/A` rather than a fabricated value (FR-4.9), **and records why** — not merely "there is a Tier-2 path", which is always true and therefore unfalsifiable
- [ ] Measured hook overhead is stated below (`docs/14_TESTING.md` §Hook overhead)

**Measured overhead:** <!-- e.g. "0.31 µs/present, game Avg FPS delta -0.2%" -->

## User-interface changes only

Skip this section if the PR does not change what the App shows. Otherwise — `ACCESSIBILITY.md` §For contributors,
`docs/08_UI.md` §Accessibility:

- [ ] Every control that shows no text of its own has a name, from `.resx` in `en`, `vi` and `ja`
- [ ] Colours come from the theme only, and nothing is shown by colour alone
- [ ] Everything new can be reached and used with the keyboard, in reading order, and Esc leaves a new dialog
- [ ] `ACCESSIBILITY.md` (and `LIMITATIONS.md`) changed in this PR if what works or what does not moved

**Checked by hand:** <!-- e.g. "Tab through the new card with the mouse unplugged; Narrator reads its name and value" -->

## Safety-string changes only

- [ ] `Safety_*` resx keys have human-reviewed `ja` (never machine-drafted — `docs/09_I18N.md`)
- [ ] Wording was not softened in any locale

## Notes for the reviewer

<!-- Anything that would be hard to see from the diff. If you resolved an item
     in docs/20_OPEN_QUESTIONS.md, say which one, mark it resolved in §S24, and move
     the entry verbatim to docs/open-questions/closed-*.md with its row in the table
     at the end of its series (beta.24, D66). -->

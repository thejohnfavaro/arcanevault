# Arcane Vault Native Windows v0.9.187 (Preview)

## Added
- Explicit **Save review with deck** action in the EDH Lab AI Review workspace. A successfully completed ChatGPT review is stored inside the same saved deck object in the verified native backup, not in an external UI cache.
- Saved reviews include response Markdown, model, review focus, UTC timestamp, and an SHA-256 fingerprint of format, commander, and card names/quantities. They reopen offline, with clickable card references when the local catalog is available.
- Saved/Outdated AI review labels in the Deck Library and saved-deck inspector.
- Commander offline report now shows recognizable fast mana, efficient tutors, denial/lock pieces, and possible combo packages. These are advisory **card-name indicators**, not automatic bracket upgrades or claims that a combo works without setup.
- Regression coverage for review recovery, preserved inventory/history, stale-deck fingerprints, optimistic SHA conflicts, and the expanded Commander indicators.

## Changed
- Editing a saved deck preserves prior AI advice but visibly flags it as outdated when format, commander, cards, or quantities differ from the reviewed snapshot. Decorative changes such as deck folder and artwork are excluded from this fingerprint.
- Stored reviews use the existing optimistic-SHA, atomic SQLite replacement, independent verification and recovery-point safeguards. A saved review is never silently written from an unsaved draft.
- Upgraded disposable Deck Library cache schema to read the new review state correctly.
- AI advice remains read-only and requires explicit connection and opt-in before sending any deck information.

## Fixed
- AI Deck Reviews no longer need to disappear when the EDH Lab view closes or the app restarts, once explicitly saved with a deck.
- The offline bracket report now offers contextual card-construction evidence alongside the rules-backed minimum, without conflating Arena Brawl with tabletop Commander.

## Removed
- No collection entries, deck histories, sync data or existing recovery files removed. Arena Brawl still has no Commander Bracket classification.

## Verification
- Full Windows WPF build, native regression suite, installer and release workflow must pass. Manual test: open a saved deck, analyze with ChatGPT, click **Save review with deck**, reopen it, then alter the card counts and verify the old review becomes **Outdated**.
- The local rules-backed minimum continues to follow the versioned Game Changer roster; strategic-speed and combo indicators are not authoritative card-text checks or a final power rating.

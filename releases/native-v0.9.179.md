# Arcane Vault Native Windows v0.9.179 (Preview)

## Added
- EDH Lab AI Review now includes a read-only `Compute bracket (offline)` action.
- Versioned local 53-card Commander Game Changers roster, with deterministic minimum bracket floor and a breakdown of matching cards including the commander.
- Native regression tests for roster count, Game Changer thresholds, and known bracket-restricting play patterns.

## Changed
- Bracket computation explicitly separates rule-backed minimum bracket from unresolved deck play-pattern and intent factors; does not present a speculative final power tier.
- Card identities are normalized independently of printing codes, and Commander is assessed separately from Arena Brawl.

## Fixed
- No changes to the AI review rendering or Xeneon integration in this development pass.

## Removed
- Nothing. Saved decks, ownership, and synchronization data are not changed by bracket checking.

## Verification
- Run the native regression checks and compile the Windows WPF app. Test EDH Lab's new offline button on Commander decks with zero, three and four or more Game Changers. Final bracket inference and persistent metadata remain follow-up work.

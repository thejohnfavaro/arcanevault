# Arcane Vault Windows v0.9.210 (Preview)

## Added
- Read-only reconciliation of historical match identities for Deck Analytics, using previously verified Arena deck-ID mappings and uniquely confirmed manual/Arena assignments.
- Clear status reporting of historical matches recovered into the selected deck and name-matched records still excluded because their attribution is unverified or conflicting.
- Regression coverage requiring telemetry, mulligan observation, and verified play/draw metrics to use the same selected match IDs as deck performance.

## Changed
- The global Deck Scope selector now resolves each recorded match against the existing safe `MatchDeckAttribution` rules, rather than requiring every historical match row to have a nonempty `native_deck_id`.
- The read-only history projection carries existing Arena ID and attribution notes for safe resolution; duplicate legacy/native matches can supplement missing historical verification metadata without replacing the native result or assignment.
- Per-deck GRE telemetry and play/draw filtering uses a single JSON-parameterized set of resolved immutable match IDs; no match rows or collection data are modified.
- All Decks remains the aggregate option, while color identity still describes the **opponent**, never the player's commander.

## Fixed
- Saved decks with historically assigned matches, including Arena Standard decks, could show no matches in Deck Analytics after the exact-native-ID-only filter was introduced. Verified historical records are now included without guessing between duplicate names or conflicting IDs.
- Telemetry summary sections use the same reconciliation instead of remaining empty when a verified historical match lacked a native-deck ID.

## Removed
- The restriction that a historical match must already contain a native-deck ID to appear in read-only, individual-deck performance views. No stored match data, W/L results, or collections are removed.

## Verification
- Run Windows CI and the `match-deck-attribution`, `arena-log-assignment`, `match-performance`, and `native-visual-shell` suites.
- Select Mono-White Auras, Borg, and All Decks and verify that scoped counts reflect their verified historical assignments, while conflicts and ambiguous older rows remain excluded.
- Verify that all observed telemetry/play-draw totals change with the selected deck; no fabricated opponent-color data or inferred W/L outcomes.
- Compilation, CI and user-device acceptance of this release are pending until a completed workflow reports success.

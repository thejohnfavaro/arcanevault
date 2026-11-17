# Arcane Vault Native Windows v0.9.145 (Preview)

## Added
- Dedicated Deck Analytics workspace in main Windows navigation. Match History is reserved for reviewing and correcting recorded matches.
- Longest win/loss streak and weekly confirmed match performance, alongside two-deck comparisons, format selectors and chronological trend charts.
- Optional bounded `matchAnalytics` block in Windows USB and Wi-Fi peer sync exports, enabling explicitly reviewed read-only match statistics on Android.
- 1.0 release readiness audit and clear acceptance gates for sync, upgrades, recovery, and real hardware.

## Changed
- Peer snapshots preserve their existing `format`, `schema`, `revision` and authoritative `data` fields. The match-statistics block is presentation-only and never enters inventory merge or baseline reconciliation.
- Match History keeps its editing and exact-ID reconciliation workflow while the new analytics workspace refreshes from the same native-first match records.

## Fixed
- Weekly totals and longest winning/losing streaks are computed only from recorded confirmed results with deduplicated match IDs.
- Low-sample labels make small win-rate samples visible.

## Removed
- The expandable Deck Performance section from Match History, superseded by its own workspace.

> Preview only. Android analytics import is opt-in and read-only, not full differential bidirectional match sync. No existing inventory, saved deck or match-history record is rewritten by the analytics export. The application is **not** certified 1.0 ready; review `docs/arcane-vault-next/ONE_ZERO_READINESS_AUDIT.md`.

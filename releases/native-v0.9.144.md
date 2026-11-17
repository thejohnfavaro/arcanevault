# Arcane Vault Native Windows v0.9.144 (Preview)

## Added
- Select an optional second deck to compare confirmed match records, win percentages and rolling trends side by side.
- Filter the performance dashboard by recorded Arena format, including Unknown format, and by last 30 days, 90 days or all time.
- Expandable Match History performance dashboard now shows the latest 20 confirmed results with deck, format, local timestamp and rolling 10-match win percentage.
- Independent second-deck trend line and percentage-point comparison.
- Regression coverage for format-scoped trends, deck comparisons, timeline ordering and unknown results.

## Changed
- The primary dashboard, comparison deck and timeline share the selected format and date range.
- Deck and format choices are preserved when Match History refreshes, where still available.

## Fixed
- Unknown match results remain excluded from win percentages when filtering by format; missing format values are grouped as Unknown format.
- Timeline and win-rate trends reflect the chosen deck and recorded Arena format rather than combining unrelated queues.

## Removed
- No features removed.

> Analytics are strictly read-only and use already recorded, assigned match results. Stream Deck Companion and XENEON EDGE continue to receive the existing native snapshot format.

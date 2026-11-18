# Arcane Vault Native Windows v0.9.151 (Preview)

## Added
- A verified Arena ranked-season countdown in Windows Deck Analytics, with the published reset time shown in the PC's local timezone.
- A verified official-season schedule cache sourced from Wizards of the Coast's ranked-season page, persisted locally for offline access.
- Automatic official schedule checks on application startup and every 12 hours while the Windows app is running; network failures retry after one hour and cannot block normal deck/match snapshots.
- Conditional next-season Constructed and Limited rank projections **only for rank transitions explicitly published in the official reset table**. These assume you finish the season at the currently observed rank.
- New optional `season`, `predictedConstructed`, and `predictedLimited` fields in the existing backward-compatible Stream Deck / Edge snapshot for XENEON EDGE widget v0.8.0.
- Regression coverage for official date parsing, offline cache, unsupported predictions and expired dates.

## Changed
- The ranked season date is no longer a per-release assumption. Windows checks https://magic.wizards.com/en/news/mtg-arena/ranked-season periodically and writes an atomic, verified cache. The current published November 30, 2026 at 20:00 UTC season end is a startup fallback **only until it expires**.
- The Current Rank display on Edge continues to show the exact Constructed and Limited ranks from Arena logs; season projections are labeled conditional and are not attributed to past match results.

## Fixed
- Once a previously confirmed season end passes, the app refuses to roll the old date forward by a month or display a negative countdown. Until Wizards publishes a new verifiable schedule, it displays that the next date is awaiting official confirmation.
- Invalid or unavailable season pages cannot overwrite a previously valid future season schedule, and network outages do not interrupt Stream Deck / Edge refreshes.

## Removed
- No data, working widgets, or integrations were removed.

This update is a **preview**, not a claim that a future date is guaranteed. While the Windows app is closed it cannot refresh its local cache; the next app launch checks again. Wizards of the Coast is the schedule authority. A separate user-initiated ChatGPT daily conditional watch can provide notifications outside the app without altering app data. Windows v0.9.151 and Edge v0.8.0 are designed to be installed together.

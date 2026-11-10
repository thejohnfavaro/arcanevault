# AVN-019 — Native Windows Set Progress checklist (read-only)

**Status:** native preview only; not integrated into the shipping Windows application.

## Workflow

1. Open `ArcaneVault.Next.Desktop.exe` and choose **Open a read-only catalog snapshot**. Select the existing locally synced `%LOCALAPPDATA%\ArcaneVault\scryfall-catalog.sqlite`.
2. Choose **View collection backup** and select the full `arcane-vault-backup.json` exported by **Data & Sync → Save full backup** in the installed Windows app.
3. Open the **Set Progress** tab. Default view shows sets containing at least one verified owned catalog collector slot, newest sets first. Enable **Include sets with 0 verified slots** to list the full catalog.
4. Select a set to see its numbered paper collector slots, sorted by number; double-click a card for offline details.

## Trust model

- Each physical backup line must supply a **valid Scryfall printing UUID** that matches the local catalog's name, set and collector hints. No UUID, bad UUID, absent catalog UUID or conflicting hints remain unresolved. Printed names are never guessed into a set.
- The confirmed UUID must be a **physical paper printing**: `variants.digital=0` and `variants.games` includes `paper`. Arena/digital-only printings are explicitly excluded.
- **Denominator:** number of **distinct collector numbers** among physical-paper printings with that set code in the current catalog; different foil finishes or multiple printing IDs at the same collector number count as **one slot**. **Numerator:** collector numbers for which at least one physical inventory row has passed all verification. **Copies:** sums of physical copies for confirmed rows only.
- This scope can include paper token, promo and art/bonus collector-number ranges. It is not a claim about booster-only base set completion or universally collectible cards. Catalog omissions or stale local Scryfall data affect denominators and eligibility.
- Every set row is counted only from a snapshot. Missing catalog => no percentage, not 0% completion. All queries open the isolated SQLite catalog read-only. No user inventory is changed.
- Verified Set Progress is currently **Windows physical only**. Name-only Arena exports cannot be credited to a specific printing or set.

## Validation

The synthetic regression covers 17 physical copies: 8 confirmed and 9 unresolved, including duplicated foil/etched copies, a digital-only print, collector conflict, a name-only card with set hints and a second printing at the same collector number. The catalog has 6 distinct paper collector slots in 3 sets (3 owned), including one 0%-owned set; the test also checks newest-first ordering, numeric collector order, parameterized set lookup and source-file hash/timestamp preservation.

## Still missing for true Windows parity

Integrated live collection reader and transactional editing, reliable Set Progress against an authoritative native store, art and set symbols, specialty set filters, user-device p95 benchmarks, full backup/restore parity and migration cutover/rollback tests. Keep the old installed app intact.

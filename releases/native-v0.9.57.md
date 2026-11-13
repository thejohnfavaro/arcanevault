# Arcane Vault Native Windows v0.9.57 — Collection import integrity audit

## Added

- **Source audit** in the CSV/Excel reconciliation review: original row count and copies, distinct normalized identities, duplicate source rows collapsed, explicit zeros, unresolved and ambiguous physical printings, preserved local-only rows/copies and projected scope totals.
- An explicit distinction between **all imported identity quantities match** and **the entire scope matches the source snapshot**. A source snapshot is not a total replacement unless there are no extra local-only holdings; printed-card quantity coverage never implies Scryfall printing verification.
- Catalog-specific unresolved printing reasons in each reconciliation line, with unresolved rows prioritized at the top of the review table.
- A mandatory, visible acknowledgement for imports containing unresolved/ambiguous physical printing rows. The import cannot apply until checked.
- **Export audit…** in the review dialog: a read-only .txt audit with source and proposed candidate SHA-256, aggregate counts and all per-identity deltas/verification statuses.

## Changed

- Both physical and Arena import routes fingerprint the selected CSV/XLSX file before and after parsing and again immediately before applying. If the source has changed since the audit preview, the import refuses to write and asks for a fresh review.
- The review shows projected post-import totals and local-only holdings, instead of presenting imported target rows as if they were the entire collection.
- Validated proposed candidate data against **every imported target identity**, not just an aggregate copy-count delta.
- Completed import messaging includes the source audit alongside the quantity reconciliation.

## Fixed

- Unresolved physical import rows could display only a generic verification message. The audit now retains the concrete catalog-verification failure (including ambiguous set/collector matches).
- Duplicate lines could be silently consolidated without making that source-file behavior visible. The audit now reports raw versus distinct identities.
- A CSV/Excel file edited between opening the review and applying a change could otherwise lead to a stale review of its earlier parsed rows. Source SHA fingerprint mismatch now blocks application.

## Removed

- No additive import compatibility entry points, native collection fields, recovery safeguards, separate Arena and physical collections, deck revisions, artwork settings or local catalog data were removed.

## Verification and safe workflow

1. Export a full native backup and verify it using the existing Readiness Checker.
2. If replacing an entire physical collection, first clear **Physical only** with the verified recovery dialog. Missing source identities are **never deleted** by ordinary reconciliation.
3. Import a full CSV/XLSX via **Collection & Deck Library → Physical Collection → Import CSV / Excel**.
4. Review **Source audit**, **Printing verification**, **Projected inventory** and **Snapshot coverage**. Expand the audit/export the report to review every identity and its exact quantity delta.
5. If unresolved/ambiguous printings exist, explicitly acknowledge that they are not verified Scryfall printings. Resolve them manually later; the importer does not invent printing IDs.
6. Apply only if the reviewed changes are correct. The original SQLite collection is retained as a full recovery point.
7. Reimport the same unchanged file: all target quantities should match, Apply should stay disabled and no additional recovery copy should be created.
8. Import a file with a changed quantity and review the exact adjustment (positive or negative). If the file changes while the review is open, close/restart: the SHA guard must reject it.
9. Verify Arena copies, saved decks, shortlist, preferences and the Scryfall catalog are unchanged after physical reimport.

**Scope:** Native Windows v0.9.57 preview. Automated test success does not replace real-device validation of the WPF dialog at the installed display scaling. This is not an Android release or the Windows 1.0 milestone.

# AVN-036 — Native catalog view modes and Excel collection import

## Display modes

The native **Card Catalog** can switch among **Detailed list**, **Compact list** and **Cards/Tiles**, while retaining the selected printing. Detailed and Compact retain virtualized scrolling. Tiles operate on a maximum of 200 results returned by the local catalog query; they are styled text cards, not an image-downloading grid. Image retrieval remains on-demand when opening Card Details, protecting responsiveness and bandwidth. The **Physical Collection** supports Detailed and Compact table views without altering ownership.

## Excel importer

In **My Collection → Physical Collection**, choose **Import Excel (.xlsx)**. The importer reads the first visible worksheet and accepts these commonly used column names:

| Column | Accepted header examples | Required |
| --- | --- | --- |
| Card name | `Name`, `Card Name`, `Card` | Yes |
| Count | `Quantity`, `Qty`, `Count`, `Copies`, `Owned` | Yes |
| Set code | `Set`, `Set Code`, `Edition` | No |
| Number | `Collector Number`, `Collector #`, `Card Number`, `Number` | No |
| Exact identity | `Scryfall ID`, `Scryfall UUID`, `UUID` | No |
| Finish | `Finish`, `Foil`, `Is Foil` | No |

Quantity must be a positive integer. Finish recognizes `nonfoil`, `foil`, `etched`, yes/no flags, and keeps missing information as `unspecified`. All source files remain unchanged.

### Catalog matching and preservation

- Scryfall IDs are checked against the **independent native local Scryfall catalog** when available. They must correspond to real paper (not digital-only) printings and the input card name/set/number must agree.
- Without an ID, **exact card name + set code + collector number** may resolve a unique catalog printing. **Name alone is insufficient** for a verified edition and is never guessed.
- Rows without verifiable printings are still imported **as unresolved physical holdings**, preserving card name, quantity, optional set hints, collector number, finish and any supplied ID. They do not increase verified Set Progress.
- Verified identical print ID + finish entries can be incremented. Unresolved entries only combine with an identical unresolved name/set/number/finish line—not with verified ownership by name.
- One workbook import is **additive**, not a replace operation. Repeating the import adds the same quantities again. The preview explicitly warns about duplicate imports.
- On approval, the native workspace uses its existing stale-SHA protection and one atomic SQLite swap; a distinct full previous-workspace recovery file is retained. Arena ownership, decks/revisions, artwork and unknown collection preferences are preserved.

### Quality gates

CI creates a real synthetic `.xlsx`, imports mixed verified and unresolved printings, checks exact conservation and unchanged ZIP/source/catalog hashes, and performs a native SQLite import with verified rollback. It also rejects bad quantities and stale database fingerprints.

### 1.0 acceptance

Use a disposable profile or a private full backup. Import a real exported workbook with known printings and at least one name-only card. Compare imported totals, choose alternate view modes, verify unresolved cards stay uncredited in Set Progress, and restore the pre-import collection from recovery. Confirm large imports, screen scaling and catalog-search responsiveness on the user's Windows PC.

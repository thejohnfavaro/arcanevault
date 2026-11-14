# Arcane Vault Native Windows v0.9.100 — full-page Data & Sync

## Added
- A dedicated full-page **Data & Sync** workspace with one sidebar navigation entry.
- Four selectable workspace sections: **Device Sync**, **Snapshots**, **Import & Export**, and **Updates**, matching Android's v2.1.8 organization.

## Changed
- **Device Sync** is shown first when opening the workspace.
- Existing Wi-Fi peer exchange, three-way comparison, reviewed inventory changes, snapshot, recovery, full backup, legacy migration, Scryfall catalog and native update controls have been moved out of expandable sidebar menus into their matching full-page sections.
- Switching sections changes presentation only and does not start any data operations.

## Fixed
- Sync and backup controls no longer occupy most of the Windows sidebar.
- A previous migration regression assertion was updated to protect the new full-page layout instead of expecting old sidebar expanders.

## Removed
- Redundant nested sidebar category menus for Data & Sync, Card Database and Native Updates. The functions themselves remain available.

## Safety
- This UI navigation update does not modify collection, deck, sync baseline, or recovery storage. Existing safeguards and explicit write approvals are preserved.

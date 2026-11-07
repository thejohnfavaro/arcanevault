# Arcane Vault for Windows — v5.17.1

## Release overview

This maintenance update fixes a SQLite error when opening individual sets in Physical Set Progress, especially after updating a physical collection CSV.

## Added

- A regression check for repeated physical set queries on SQLite connections reused from the connection pool.

## Changed

- Physical Set Progress rebuilds its temporary ownership snapshot for each request, reflecting the currently imported physical CSV without touching stored Arena ownership.
- The temporary table is explicitly dropped before creation, which handles pooled SQLite connections that retain temporary objects between requests.

## Fixed

- **SQLite Error 1: 'table physical_ownership already exists'.** Opening the new Star Trek set—or another set—after navigating Physical Set Progress no longer attempts to create an existing temporary table.
- Stale physical quantities from a previous request cannot remain in the temporary ownership table when a fresh physical inventory is loaded.

## Removed

- Nothing. The Arena/Physical toggle, collection imports, set sorting, filters, and drilldowns remain available.

## Data safety

- Existing Arena and physical collection data is preserved.
- No CSV reimport or database reset is required after applying the hotfix.
- Set completion remains based on unique card names, not exact set printings.

## Installing

In Arcane Vault, open **Import & Data → Application updates → Check for updates**, then download and install v5.17.1. The Windows release ZIP includes a SHA-256 checksum.

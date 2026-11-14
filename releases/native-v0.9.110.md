# Arcane Vault Native Windows v0.9.110 — Peer Sync Safety

## Added
- Regression coverage for malformed physical inventory entries in peer comparison.

## Changed
- Peer sync now requires a card name or Scryfall printing ID for each physical inventory record before comparison or merge.

## Fixed
- A fully anonymous physical entry previously generated a nonempty length-prefixed composite key that could be mistaken for a valid identity and could conflate unrelated inventory rows.

## Removed
- No inventory, deck, artwork, catalog or backup data removed. Peer sync remains explicitly review-driven.

## Safety
- Invalid anonymous peer records are rejected before they can affect a merge. Existing collections and the local database remain untouched.

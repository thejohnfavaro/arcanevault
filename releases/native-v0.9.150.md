# Arcane Vault Native Windows v0.9.150 (Preview)

## Added
- Cross-process-safe peer-device identity creation: the Windows launcher and native application now use atomic create-once UUID persistence instead of potentially overwriting an existing file.
- Regression tests for simultaneous UUID initialization, corrupted/truncated identities, and mismatched or malformed saved peer checkpoints.
- Structural verification of stored baseline peer snapshots (format, schema, required groups, revision metadata and device identity) before offering them for merge review.

## Changed
- A damaged peer identity or checkpoint is explicitly refused. It is never silently replaced with a new identity, which could invalidate an existing Windows/Android sync relationship.
- Existing merge conflict choices, first-pair approval, local backup, collection, deck and artwork storage remain unchanged.

## Fixed
- A concurrent app/launcher startup no longer risks generating two competing Windows device UUIDs.
- A checkpoint belonging to a different peer, or lacking a valid peer envelope, can no longer be loaded as a verified baseline.

## Removed
- No data or features removed.

This is a **sync reliability preview**, not a completed 1.0 certification. There is still no authenticated pairing, unreviewed deletion, or automatic conflict resolution. Android counterpart: **v2.1.62**.

To test: update normally and review Data & Sync, including saved baselines and existing device comparison. Your peer identity file is deliberately preserved. Never delete the device identity file to troubleshoot; that would break the continuity of remembered checkpoints.

# Arcane Vault Native Windows v0.9.65 — Inline card browsing for Deck Library & EDH Lab

## Added

- **Saved Deck Library — inspect without opening Deck Studio.** Selecting a saved deck opens a docked, read-only deck-contents panel. Selecting any card in that list shows its cached official card art, card type, WUBRG/colorless identity symbols, full Oracle rules text with inline official mana symbols, and a useful copy-count summary. Choose another card or deck to update the same inspector.
- **EDH Lab commander discovery — persistent card inspector.** Selecting a commander updates its preview beside the existing virtualized candidate list. The inspector uses the local Scryfall catalog and commander record, never silently assumes the commander or its printing is owned. Random commander selection updates the same preview.
- **Reusable native card inspector** shared by both browsing views. It loads data only for the selected name, cancels stale results with a version guard, uses cached Scryfall card artwork, and has a **Full details & printings** action for explicitly opening the existing detail window.
- **Read-only deck outline model.** Shows saved mainboard quantities and commander designation without adding an extra phantom copy when the commander is represented separately. Unsupported imported entries remain explicitly identified as potentially incomplete.
- **Show card preview** toolbar action for reopening the deck inspector after hiding it, without changing selected deck or triggering edits.
- Regression coverage for commander-inclusive vs separate commander deck outlines, preserved quantities, stale-selection guards, WPF preview wiring and virtualization.

## Changed

- The Deck Library's large illustrated deck boxes remain on the left while the selected deck's card list and inspector occupy an optional right column. Close the preview to reclaim horizontal space.
- EDH Lab's commander browser grows moderately on desktop to accommodate an inline preview, while keeping its paging, inventory-only filter, format selection, color matching, random commander and **Build with selected commander** workflow.
- Both previews use the already-installed **official Scryfall symbol** renderer and the unboxed Oracle reading style from v0.9.64.
- **Ownership labels distinguish saved deck quantities from recorded physical/Arena name-level ownership.** They are not printing verification or evidence of legality; format and inventory safety checks are unchanged.
- The full Card Details window opened through the preview is deliberately read-only with respect to the native collection. Saving decks, altering inventory, changing preferences, or deleting data still use their existing verified workflows.

## Fixed

- Inspecting a saved deck's card names previously required opening the separate Deck Studio window.
- Commander browsing previously showed only card name, type and color-identity letters in list rows; inspecting art or Oracle text required leaving the selection flow.
- Switching between cards while a metadata query was underway could otherwise show information from an earlier selection.
- A separately stored commander could previously be misrepresented as an additional mainboard copy by a naive deck preview.

## Removed

- No owned cards, deck data, printings, preferences, recovery backups, or edition-specific metadata were removed.
- No new automatic network prefetching, new synchronization process, or collection write paths were added.

## Windows acceptance

1. Open **Collection & Deck Library → Deck Library**. Select a saved deck: its preview should show a list of actual saved cards on the right, including accurate quantities and a clearly designated commander.
2. Select different deck entries. Artwork, type, mana identity and Oracle text should update in-place without opening an editor or changing saved deck contents. Check split card names and long rules text.
3. Hide the deck preview using **×**, then select **Show card preview** to reopen it without choosing another deck.
4. Browse a deck with an unsupported imported entry. The preview must disclose that the displayed entries may be incomplete rather than claiming complete ownership or a verified printing.
5. Open **EDH Lab** and select different commanders. The inspector should update beside the list; paging, search, color filters, Arena Brawl mode, Recorded Only and Surprise Me! must continue working.
6. View WUBRG, colorless and multicolor commanders; inspect official mana-symbol artwork in Oracle text. Previously cached glyphs should remain available offline, with literal-token fallback otherwise.
7. Select **Full details & printings** from either preview. It should open only when metadata is available, in read-only collection mode, without silently editing ownership or preferred artwork.
8. Rapidly select several different cards and commanders. The final details must correspond to the last selected name; no stale async data.
9. Confirm saved decks, owned inventory, recovery points and shortlist are unchanged by simply browsing. Deck Studio and EDH Lab saved-deck edits still require their existing guards.
10. Check window resizing and 125–200% DPI scaling; list virtualization and existing deck/commander search performance should remain intact.

**Scope:** Native Windows preview v0.9.65. This release extends the in-place inspection pattern to additional browsing screens; native Deck Studio already had card preview and remains unchanged. Automated checks validate wiring and quantity invariants; on-device UI acceptance is still important.

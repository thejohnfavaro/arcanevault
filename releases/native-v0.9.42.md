# Arcane Vault Native Windows v0.9.42 — EDH Lab Analysis Scroll-Viewport Hotfix

Fixes a layout regression introduced by the tabbed EDH Lab workspace in
v0.9.41: expanding multiple analysis sections could push the lower controls
beyond the visible drawing area, with no obvious way to reach them.

## Added

- Always-visible vertical scrollbars in the **Mana & Roles** and
  **Improve** tabs so that users can discover and scroll to the bottom
  when several sections are expanded.
- A resize-aware maximum content viewport for both analysis tabs,
  derived from the real tab control height minus its header and borders.
  The viewport is recomputed on window sizing and center-pane splitter
  layout changes.
- Native regression assertions ensuring the named scrollers stay in
  their corresponding tabs and the resize handler continues to bound
  their height.

## Changed

- Both analysis sections now grow *inside* the scrollable tab viewport.
  Expanding multiple sections increases scrollable content height rather
  than letting the content expand beyond the application draw area.
- Touch-style vertical panning is supported on both scroll regions.
- The **Cards** tab, separate decklist, card preview, and footer retain
  their established layouts and functionality.

## Fixed

- The lower mana-curve controls and bottom **Swap Lab** review / apply
  controls are reachable by scrolling even with several expanders open.
- Tab scrolling responds to window resize and resizable column changes
  instead of retaining a stale unconstrained content height.

## Removed

- Nothing. No collection migration, card change, saved-deck edit,
  import reconciliation change, or modification to existing native
  backup/recovery safeguards.

## Local acceptance

1. Update from native v0.9.41 to **v0.9.42** using Native Updates.
2. Open a saved EDH Lab deck and switch to **Mana & Roles**.
3. Expand **Arena-style land balancing** and **Mana curve & casting
   consistency** at the same time. Scroll to the end; land controls,
   curve details and blueprint must remain accessible inside the pane.
4. Switch to **Improve**. Expand **Collection-aware deck optimization**
   and **Swap Lab** together; scroll to the bottom to reach the preview
   and Apply button.
5. Resize the window and drag the center-column splitters. Verify
   scrolling still works and the decklist/card-preview stay in view.
6. Check the **Cards** tab remains usable and ordinary saved-deck
   editing is unaffected.
7. Cancel without saving. Neither the collection nor stored deck
   revisions should change from viewing or scrolling.

Release requires normal Windows build, native WPF regression suite,
checksum ZIP publication and local UI acceptance. This is a preview,
not a signed 1.0 installer.

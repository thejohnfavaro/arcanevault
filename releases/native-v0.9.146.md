# Arcane Vault Native Windows v0.9.146 (Preview)

## Added
- Approved Arcane Vault vault-door emblem and installer art sources tracked with the Windows application.
- Deterministic build step generating multi-resolution Windows icons and matching full-size Inno Setup wizard artwork.
- A consistent set of original vector glyphs for all primary navigation destinations and key utility controls, replacing assorted Unicode placeholder symbols.
- Brand implementation and acceptance documentation covering Windows taskbar, Start Menu, installer, launcher and high-DPI UI behavior.

## Changed
- Windows application icon, titlebar/taskbar mark, installer executable, pinned launcher, installed-program shortcuts, and uninstall display icon now share the same approved vault identity.
- Windows installer welcome illustration and small header badge use the matching artwork.
- Main navigation labels remain plain accessible text; brand glyphs are vector path artwork rather than borrowed game iconography.

## Fixed
- Avoid icon inconsistencies between the Windows desktop executable and permanent launcher when new preview versions are installed side by side.
- Regenerate and validate all build-time icon/installer resources before release, rejecting missing approved source art.

## Removed
- Unrelated Unicode emblem characters from the primary Windows navigation and common utility actions.

> This is a branding-only preview. Collection data, Arena match history, peer sync, deck editors, Stream Deck and XENEON EDGE logic are unchanged. Actual taskbar icon caching and high-DPI installer appearance still require local Windows review.

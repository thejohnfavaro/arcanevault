# Arcane Vault Windows branding implementation

## Art direction

Approved direction: dark bronze/iron vault door, ember-glowing keyhole and restrained
gold engraving. The logo and wizard art are **original Arcane Vault branding**,
not official MTG or other game iconography.

Repository-approved source images:
- `ArcaneVault.Next.Desktop/Assets/BrandSource/vault-icon.webp` — simplified vault-keyhole launcher emblem.
- `installer/brand/vault-wizard.webp` — approved fantasy archive/vault scene used for installer side art.

These small lossily compressed source masters exist to keep executable and setup
builds deterministic. The high-resolution originals should be retained separately
in the brand working art archive for future marketing exports.

## Generated resources (do not hand-edit)

Run `python -m pip install pillow==11.3.0` and
`python tools/build_windows_brand.py` before building Windows locally.

| Output | Used by |
| --- | --- |
| `ArcaneVault.Next.Desktop/Assets/ArcaneVault.ico` | Desktop executable, launcher executable, setup executable, shortcuts and installed-app branding |
| `ArcaneVault.Next.Desktop/Assets/ArcaneVaultIcon.png` | Main window taskbar/window icon and in-app sidebar brand mark |
| `installer/brand/ArcaneVaultWizard.bmp` | Inno Setup wizard illustration |
| `installer/brand/ArcaneVaultWizardSmall.bmp` | Inno Setup wizard compact/header mark |
| `installer/brand/ArcaneVaultWizardHeader.bmp` | Extra wide installer header visual for adjacent packaging/marketing |
| `ArcaneVault.Next.Desktop/Assets/ArcaneVaultBrandBanner.png` | Optional banner for welcome/about surfaces |

CI generates these before `dotnet restore`, verifies multi-size .ico frames,
builds both executables (including the pinned launcher), packages the Inno
installer and publishes the preview only after test success. If any source
asset is missing, CI must fail rather than silently use a previous icon.

## UI iconography

The Windows sidebar and key toolbar actions use `VaultBrandedNavContent`
from `App.xaml`: a 20px theme-aware outlined vector glyph and **plain text**
label. Glyph geometry is assigned through each button's `Tag`; only buttons
that use `Tag` for icon data should use this template. Keep any button's
semantic tag untouched.

Design metaphors:
- Archive card = Card Catalog
- Starred marker = Shortlist
- Storage box = Collection
- Progress ledger = Set Progress
- Stacked pages = Deck Library
- Bars = Deck Analytics
- Record page = Match History
- Open grimoire = EDH Lab
- Exchanging arrows = Data & Sync
- Clock, tick, import/export arrow, return arrow, refresh and archive
  = common utility actions

Do not replace official **card data**, real card set symbols or mana symbols
with brand marks: those belong to card metadata, not app brand identity.

## Acceptance checklist

- Windows File Explorer, taskbar, title bar, Alt+Tab, Start Menu, desktop
  shortcut and Installed Apps/Uninstall entry use the **new vault mark**.
- Launcher icon remains correct after a side-by-side update from a previous
  version without requiring a new folder.
- Installer presents the approved side artwork and compact vault seal.
- Every primary workspace nav control has readable text, an original vector
  glyph and the same hover/keyboard focus semantics as before.
- High DPI 100/150/200%, 16/24/32px icons, long localization strings, and
  high-contrast/theme variants are inspected on an actual Windows desktop.
- User workspace, deck collection, data synchronization and Stream Deck
  statistics are not modified by this purely visual release.

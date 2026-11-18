#!/usr/bin/env python3
"""Deterministic Windows brand rasterizer.

The approved WEBP source art is tracked in Git. Raster outputs are generated
before dotnet builds: WPF icon resource, Windows multi-resolution .ico and the
Inno Setup wizard images. Never write to user data or workspace directories.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import argparse

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "ArcaneVault.Next.Desktop" / "Assets"
INSTALLER = ROOT / "installer" / "brand"
GOLD = (226, 170, 84, 255)
EMBER = (249, 102, 38, 255)
CHARCOAL = (17, 16, 19, 255)

def font(size: int):
    for path in (
        Path("C:/Windows/Fonts/georgiab.ttf"),
        Path("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf"),
    ):
        if path.is_file():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()

def fit_icon(source: Image.Image, size: int) -> Image.Image:
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    icon = source.copy()
    # For 16px/24px icons, a clean, uncomplicated mark is clearer than tiny
    # rivets or fine shading from the original large-format artwork.
    if size <= 24:
        scale = 4
        drawing = Image.new("RGBA", (size * scale, size * scale), (0, 0, 0, 0))
        d = ImageDraw.Draw(drawing)
        cx = cy = size * scale / 2
        outer = int(size * scale * .47)
        d.ellipse((cx-outer, cy-outer, cx+outer, cy+outer),
                  fill=(36, 29, 28, 255), outline=GOLD, width=max(3, scale*2))
        inner = int(size * scale * .30)
        d.ellipse((cx-inner, cy-inner, cx+inner, cy+inner),
                  outline=EMBER, width=scale*2)
        k = int(size * scale * .09)
        d.ellipse((cx-k, cy-int(size*scale*.12)-k,
                   cx+k, cy-int(size*scale*.12)+k), fill=GOLD)
        d.polygon([(cx-int(size*scale*.085),cy-int(size*scale*.06)),
                   (cx+int(size*scale*.085),cy-int(size*scale*.06)),
                   (cx+int(size*scale*.15),cy+int(size*scale*.19)),
                   (cx-int(size*scale*.15),cy+int(size*scale*.19))], fill=GOLD)
        return drawing.resize((size, size), Image.Resampling.LANCZOS)
    max_size = int(size * .98)
    icon.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)
    canvas.alpha_composite(icon, ((size-icon.width)//2, (size-icon.height)//2))
    return canvas

def make_assets() -> None:
    icon_path = ASSETS / "BrandSource" / "vault-icon.webp"
    wizard_path = INSTALLER / "vault-wizard.webp"
    if not icon_path.is_file() or not wizard_path.is_file():
        raise FileNotFoundError("Approved source art missing. Refusing to build with placeholder graphics.")
    source_icon = Image.open(icon_path).convert("RGBA")
    art = Image.open(wizard_path).convert("RGB")
    icon = fit_icon(source_icon, 256)
    icon.save(ASSETS / "ArcaneVaultIcon.png")
    # Pillow assembles a Windows .ico with PNG frames at multiple Windows
    # shell sizes. Explicitly verify its directory before using in CI.
    icons = [fit_icon(source_icon, n) for n in (16, 24, 32, 48, 64, 128, 256)]
    icon.save(ASSETS / "ArcaneVault.ico", format="ICO",
              sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
    with Image.open(ASSETS / "ArcaneVault.ico") as check:
        if not {16,32,48,256}.issubset({n[0] for n in check.info.get("sizes", set())}):
            raise RuntimeError("Windows icon is missing required icon frames.")
    # Installer accepts Windows BMP wizard graphics. The asset is a preserved
    # crop of the approved installation artwork, not an unrelated new logo.
    wizard = art.resize((164,314), Image.Resampling.LANCZOS)
    wizard.save(INSTALLER / "ArcaneVaultWizard.bmp", format="BMP")
    small = Image.new("RGBA", (55,55), CHARCOAL)
    small.alpha_composite(fit_icon(source_icon, 51), (2,2))
    small.convert("RGB").save(INSTALLER / "ArcaneVaultWizardSmall.bmp", format="BMP")
    banner = Image.new("RGBA", (493,58), CHARCOAL)
    draw = ImageDraw.Draw(banner)
    draw.rectangle((0,0,492,57), outline=(91,60,39,255), width=1)
    banner.alpha_composite(fit_icon(source_icon, 49), (8,4))
    draw.text((72,10), "ARCANE VAULT", fill=GOLD, font=font(25),
              stroke_width=1, stroke_fill=(60,27,14,255))
    draw.text((73,41), "YOUR COLLECTION  •  YOUR VAULT",
              fill=(168,143,121,255), font=font(9))
    banner.convert("RGB").save(INSTALLER / "ArcaneVaultWizardHeader.bmp", format="BMP")
    banner.save(ASSETS / "ArcaneVaultBrandBanner.png")
    print("Generated verified ICO, PNG and installer BMP assets from approved brand sources.")

if __name__ == "__main__":
    make_assets()

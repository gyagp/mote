"""Generate the executable icon from the same simple monitor motif as the widget."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
assets = root / "assets"
assets.mkdir(exist_ok=True)
image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((8, 8, 248, 248), radius=64, fill="#202827", outline="#485B53", width=3)
draw.rounded_rectangle((45, 59, 211, 166), radius=13, fill="#2B4036", outline="#9BE4C3", width=10)
draw.line((128, 170, 128, 194), fill="#9BE4C3", width=10)
draw.rounded_rectangle((94, 190, 162, 200), radius=5, fill="#9BE4C3")
image.save(assets / "mote.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

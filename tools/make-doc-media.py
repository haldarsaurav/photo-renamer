"""Package real app captures as documentation images and a paced GIF.

Requires Pillow for documentation maintenance only, not for building the app.
Input captures live in the ignored .build/media-frames directory.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[1]
FRAMES = ROOT / ".build" / "media-frames"
OUTPUT = ROOT / "docs" / "images"
OUTPUT.mkdir(parents=True, exist_ok=True)

for source, target in (
    ("01-photo-end", "photos.png"),
    ("03-photo-start", "number-at-start.png"),
    ("08-folder-preview", "folders.png"),
    ("09-help-safety", "help.png"),
):
    with Image.open(FRAMES / f"{source}.jpg") as capture:
        picture = capture.convert("RGB")
        picture.info.clear()
        picture.save(OUTPUT / target, optimize=True)

STEPS = (
    ("01-photo-end", "Preview every new photo name", "Two sample folders. Each folder starts numbering at 1.", 2300),
    ("02-position-menu", "Choose where the number goes", "Keep the number at the end, or move it to the start.", 1200),
    ("03-photo-start", "Watch the preview update", "The original files have not changed yet.", 2300),
    ("04-confirm-dialog", "Review the confirmation", "The actual confirmation dialog, enlarged for readability.", 2000),
    ("05-photos-renamed", "Rename the sample batch", "Eight generated photos renamed across two folders.", 2500),
    ("06-undo-dialog", "Undo the last batch", "The actual Undo dialog confirms the restoration.", 1600),
    ("07-photos-restored", "Original names restored", "Undo brings all eight sample filenames back.", 2300),
    ("08-folder-preview", "Rename folders with a shared name", "Photo collection_01, Photo collection_02, and onward.", 2800),
    ("09-help-safety", "Get guidance inside the app", "Help explains both modes, privacy, Undo and recovery.", 2000),
)

WIDTH, VIEW_HEIGHT, CAPTION_HEIGHT = 1100, 804, 96
BACKGROUND = (22, 22, 23)


def font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    filename = "IBMPlexSans-Medium.ttf" if bold else "IBMPlexSans-Regular.ttf"
    candidate = ROOT / "assets" / "fonts" / filename
    try:
        return ImageFont.truetype(str(candidate), size)
    except OSError:
        return ImageFont.load_default(size=size)


title_font, description_font, step_font = font(25, True), font(18), font(16, True)
scenes: list[Image.Image] = []
durations: list[int] = []
for index, (source, title, description, duration) in enumerate(STEPS, 1):
    scene = Image.new("RGB", (WIDTH, VIEW_HEIGHT + CAPTION_HEIGHT), BACKGROUND)
    with Image.open(FRAMES / f"{source}.jpg") as capture:
        image = capture.convert("RGB")
        if "dialog" in source:
            image = image.resize((image.width * 2, image.height * 2), Image.Resampling.LANCZOS)
        else:
            image = ImageOps.contain(image, (WIDTH, VIEW_HEIGHT), Image.Resampling.LANCZOS)
        scene.paste(image, ((WIDTH - image.width) // 2, (VIEW_HEIGHT - image.height) // 2))
    draw = ImageDraw.Draw(scene)
    draw.line((0, VIEW_HEIGHT, WIDTH, VIEW_HEIGHT), fill=(47, 47, 49), width=1)
    draw.rounded_rectangle((20, VIEW_HEIGHT + 23, 77, VIEW_HEIGHT + 66), radius=8, fill=(38, 38, 40))
    draw.text((31, VIEW_HEIGHT + 34), f"{index:02d}", font=step_font, fill=(223, 182, 108))
    draw.text((96, VIEW_HEIGHT + 16), title, font=title_font, fill=(237, 235, 230))
    draw.text((97, VIEW_HEIGHT + 54), description, font=description_font, fill=(177, 177, 180))
    scenes.append(scene)
    durations.append(duration)

# One shared palette keeps the UI colors stable between frames.
palette_sheet = Image.new("RGB", (275 * 3, 225 * 3))
for index, scene in enumerate(scenes):
    palette_sheet.paste(scene.resize((275, 225)), ((index % 3) * 275, (index // 3) * 225))
palette = palette_sheet.quantize(colors=192, method=Image.Quantize.MEDIANCUT)
animation: list[Image.Image] = []
timings: list[int] = []
for index, scene in enumerate(scenes):
    animation.append(scene.quantize(palette=palette, dither=Image.Dither.NONE))
    timings.append(durations[index])
    if index + 1 < len(scenes):
        for amount in (0.33, 0.67):
            transition = Image.blend(scene, scenes[index + 1], amount)
            animation.append(transition.quantize(palette=palette, dither=Image.Dither.NONE))
            timings.append(100)

animation[0].save(
    OUTPUT / "walkthrough.gif",
    save_all=True,
    append_images=animation[1:],
    duration=timings,
    loop=0,
    optimize=True,
    disposal=1,
)
for file in sorted(OUTPUT.iterdir()):
    print(f"{file.name}: {file.stat().st_size:,} bytes")

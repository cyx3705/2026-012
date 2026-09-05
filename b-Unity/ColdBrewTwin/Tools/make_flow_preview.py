"""Assemble actual Unity capture frames into a portable animation preview."""
from pathlib import Path
import argparse
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("--studio", action="store_true")
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
capture_directory = "AppearanceSmoke" if args.studio else "FlowSmoke"
sources = sorted((root / "Builds" / capture_directory / "frames").glob("*.png"))
if not sources:
    raise SystemExit("Run the player with --flow-smoke first.")
frames = []
for source in sources:
    with Image.open(source) as capture:
        frame = capture.convert("RGB").resize((960, 540), Image.Resampling.LANCZOS)
        frames.append(frame.quantize(colors=128))
output = root / "Docs" / ("StudioFlow.gif" if args.studio else "WaterFlow.gif")
frames[0].save(output, save_all=True, append_images=frames[1:], duration=200,
               loop=0, optimize=False, disposal=2)
with Image.open(output) as check:
    assert check.n_frames == len(frames)
    assert check.size == (960, 540)
print(f"Verified {len(frames)} frames, {len(frames) * .2:.1f} seconds: {output}")

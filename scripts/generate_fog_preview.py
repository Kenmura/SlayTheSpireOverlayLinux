from PIL import Image, ImageDraw, ImageFont
import math
import os

width, height = 1024, 1024
img = Image.new("RGBA", (width, height), (14, 16, 24, 255))
draw = ImageDraw.Draw(img)

# Background subtle parchment/atmospheric grid
for y in range(0, height, 40):
    alpha = int(12 + 8 * math.sin(y / 100.0))
    draw.line([(0, y), (width, y)], fill=(30, 36, 52, alpha), width=1)

for x in range(0, width, 40):
    alpha = int(12 + 8 * math.cos(x / 100.0))
    draw.line([(x, 0), (x, height)], fill=(30, 36, 52, alpha), width=1)

# Draw Map Graph nodes & edges
# Floor 0: start node (passed)
# Floor 1: passed node & branch
# Floor 2: current player position
# Floor 3: next step (visible)
# Floors 4-7: shrouded in dark fog
# Top: Act Boss (exposed through fog)

nodes = [
    # Floor 0
    (width * 0.5, 900, "passed", "START"),
    # Floor 1
    (width * 0.38, 780, "passed", "MONSTER"),
    (width * 0.62, 780, "passed", "EVENT"),
    # Floor 2 (Current)
    (width * 0.38, 660, "current", "ELITE"),
    (width * 0.62, 660, "passed", "REST"),
    # Floor 3 (Next Step - Visible)
    (width * 0.32, 530, "revealed", "TREASURE"),
    (width * 0.48, 530, "revealed", "MONSTER"),
    # Floor 4 (Shrouded in fog)
    (width * 0.30, 400, "fogged", "?"),
    (width * 0.50, 400, "fogged", "?"),
    (width * 0.70, 400, "fogged", "?"),
    # Floor 5 (Deep fog)
    (width * 0.38, 280, "fogged", "?"),
    (width * 0.62, 280, "fogged", "?"),
    # Floor 6 (Boss - Visible at top)
    (width * 0.5, 140, "boss", "BOSS"),
]

edges = [
    ((width * 0.5, 900), (width * 0.38, 780), True),
    ((width * 0.5, 900), (width * 0.62, 780), True),
    ((width * 0.38, 780), (width * 0.38, 660), True),
    ((width * 0.62, 780), (width * 0.62, 660), True),
    ((width * 0.38, 660), (width * 0.32, 530), True),
    ((width * 0.38, 660), (width * 0.48, 530), True),
    # Fogged edges
    ((width * 0.32, 530), (width * 0.30, 400), False),
    ((width * 0.48, 530), (width * 0.50, 400), False),
    ((width * 0.30, 400), (width * 0.38, 280), False),
    ((width * 0.50, 400), (width * 0.62, 280), False),
    ((width * 0.38, 280), (width * 0.5, 140), False),
    ((width * 0.62, 280), (width * 0.5, 140), False),
]

# Draw paths
for p1, p2, active in edges:
    color = (180, 160, 120, 200) if active else (50, 58, 78, 120)
    width_line = 4 if active else 2
    draw.line([p1, p2], fill=color, width=width_line)

# Draw nodes
for x, y, state, label in nodes:
    r = 24 if state != "boss" else 38
    if state == "boss":
        fill = (185, 45, 55, 255)
        outline = (255, 120, 130, 255)
    elif state == "current":
        fill = (220, 180, 50, 255)
        outline = (255, 235, 120, 255)
    elif state == "passed":
        fill = (80, 95, 120, 220)
        outline = (130, 150, 180, 240)
    elif state == "revealed":
        fill = (45, 140, 185, 240)
        outline = (110, 200, 245, 255)
    else: # fogged
        fill = (22, 26, 36, 180)
        outline = (40, 48, 65, 200)

    draw.ellipse([x - r, y - r, x + r, y + r], fill=fill, outline=outline, width=3)

# Draw Dark Fog Overlay Layer
fog_layer = Image.new("RGBA", (width, height), (0, 0, 0, 0))
fog_draw = ImageDraw.Draw(fog_layer)

# Solid dark fog covering floors 4-5
fog_draw.rectangle([0, 210, width, 480], fill=(8, 10, 18, 235))

# Gradient fade from y=480 to y=520
for y in range(480, 520):
    pct = (y - 480) / 40.0
    alpha = int(235 * (1.0 - pct))
    fog_draw.line([(0, y), (width, y)], fill=(8, 10, 18, alpha), width=1)

# Gradient fade from y=210 down to y=180
for y in range(180, 210):
    pct = (y - 180) / 30.0
    alpha = int(235 * pct)
    fog_draw.line([(0, y), (width, y)], fill=(8, 10, 18, alpha), width=1)

img = Image.alpha_composite(img, fog_layer)
draw = ImageDraw.Draw(img)

# Re-draw Boss node and banner cleanly above fog
bx, by = width * 0.5, 140
br = 42
draw.ellipse([bx - br, by - br, bx + br, by + br], fill=(195, 45, 55, 255), outline=(255, 160, 170, 255), width=4)

# Header Title Banner
draw.rectangle([120, 28, width - 120, 84], fill=(12, 15, 22, 230), outline=(200, 165, 80, 240), width=2)
# Subtitle tags banner at bottom
draw.rectangle([80, height - 80, width - 80, height - 28], fill=(12, 15, 22, 230), outline=(75, 140, 210, 240), width=2)

try:
    font_large = ImageFont.truetype("/usr/share/fonts/dejavu-sans-fonts/DejaVuSans-Bold.ttf", 36)
    font_med = ImageFont.truetype("/usr/share/fonts/dejavu-sans-fonts/DejaVuSans-Bold.ttf", 22)
    font_small = ImageFont.truetype("/usr/share/fonts/dejavu-sans-fonts/DejaVuSans.ttf", 18)
except:
    font_large = font_med = font_small = None

if font_large:
    draw.text((width * 0.5, 56), "FOG OF WAR MAP", fill=(255, 220, 110), font=font_large, anchor="mm")
    draw.text((bx, by), "BOSS", fill=(255, 255, 255), font=font_med, anchor="mm")
    draw.text((width * 0.5, 340), "🌫 DARK FOG OVERLAY (30% - 100% OPACITY)", fill=(140, 165, 195), font=font_med, anchor="mm")
    draw.text((width * 0.5, height - 54), "Passed Nodes Exposed  •  Act Boss Visible  •  Configurable Vision & Opacity", fill=(190, 225, 255), font=font_small, anchor="mm")

img.save("workshop-fog/image.png", "PNG")
print("Saved workshop-fog/image.png successfully")

# Color Merge Exit — UI/Icon Sprite Sheet — Image-Generation Context

## ⚠️ EXACT GRID THE GAME READS (must match, or icons land wrong)
The code slices `Assets/Resources/ui_sheet.png` as a **2048×2048** sheet, **8 columns × 8 rows**
(256px cells), origin **top-left**. It reads these cells (col,row) — keep each icon centered in
its cell, same order:

```
row0 (round nav buttons):  (0,0)HOME  (1,0)RESTART/retry  (2,0)UNDO  (3,0)SETTINGS/gear
                           (4,0)CLOSE ✕  (5,0)PAUSE  (6,0)PLAY ▶  (7,0)NEXT ▶|
row1 (square item buttons):(0,1)SOUND-ON  (1,1)SOUND-OFF  (2,1)HINT 💡  (3,1)ADD-TIME ⏰+
                           (4,1)shuffle  (5,1)bomb  (6,1)FORCE-SPLIT ◄►  (7,1)"?"
row2 (markers):            (0,2)star-gold  (1,2)star-empty  (2,2)padlock  (3,2)splitter ◄►
                           (4,2)door-bar  (5,2)"x3" badge  (6,2)"+15" badge
row3-4:                    jelly blocks (reference only — blocks are drawn procedurally in-game)
```
**Cells the game actually uses:** HOME(0,0) RESTART(1,0) UNDO(2,0) · SOUND-ON(0,1) SOUND-OFF(1,1) ·
HINT(2,1) ADD-TIME(3,1) FORCE-SPLIT(6,1). Those MUST be in place; the rest are nice-to-have.

**If the AI can't hold a clean grid** (most can't): instead generate each icon as its **own
transparent PNG** and send them to me — I'll composite them into the exact 2048×2048 grid so it
aligns perfectly. That's the reliable path.

---


A single transparent-background PNG sprite sheet of all UI buttons, icons and game pieces,
matching the game's current bright-pastel, glossy-jelly casual-puzzle look. Paste the prompt
below into an image generator (GPT-Image / DALL·E / Midjourney / Ideogram, etc.).

---

## COPY-PASTE PROMPT (English)

> A single game UI asset sheet on a **fully transparent background (PNG, alpha)**, for a cute
> **bright pastel casual mobile puzzle game**. Art style: **glossy 3D "jelly / candy" plastic**,
> soft rounded shapes, thick smooth bevels, a **soft top-left highlight** and gentle bottom
> shading, subtle inner glow, no harsh outlines, friendly and playful — like Toon Blast / Candy
> Crush UI. Consistent single top-left light source and consistent corner radius across every
> item. Clean vector-like rendering, crisp edges, no photo textures, no text/letters baked in
> unless specified. Lay every element out on a **neat evenly-spaced grid with generous padding**
> between items, each item centered in its own cell, all at a consistent visual scale.
>
> Include these items, grouped by row:
>
> **Row 1 — round glossy nav buttons (circular candy buttons):** Home (house), Retry (circular
> refresh arrow), Undo (curved back arrow), Settings (gear), Close (X), Pause (‖), Play (▶),
> Next (▶| forward). Each a plump glossy circle with a soft rim-light and a white symbol.
>
> **Row 2 — sound + item buttons (rounded-square candy buttons):** Sound-On (speaker with
> waves), Sound-Off (speaker muted), Hint (glowing lightbulb), Add-Time (alarm clock with a small
> "+"), Shuffle (two crossing arrows), Bomb, Magnet. Rounded-square glossy buttons.
>
> **Row 3 — game markers/icons:** gold Star (filled), empty/grey Star, Padlock (white padlock
> with a soft dark rim so it reads on any color), Splitter mark (two arrows pointing outward
> ◄ ►), colored Door/gate bar. Small badge pill (for counts, e.g. "x3").
>
> **Row 4 — glossy jelly blocks (rounded squares, the puzzle pieces), one per color:** red,
> blue, yellow, green, purple, orange, pink, lime. Flat top-down, soft top highlight, jelly sheen.
>
> **Row 5 — FX bits:** a soft sparkle/4-point star, a burst of tiny confetti dots, a hollow glow
> ring/shockwave, a plus-time "+15" burst.
>
> Palette (use exactly): red #F5383D, blue #2185FF, yellow #FFD417, green #29CC5C,
> purple #9947EB, orange #FF871A, pink #FF66B8, lime #A8E61F, teal #0DC7BD, brown #8F5E38.
> Button base tints: home-blue #2185FF, retry-red #F5383D, undo-purple #9947EB, gear-grey #AEB4C2,
> hint-gold #FABD3D, add-time-green #4CBD85. UI accent / symbols: white #FFFFFF with a soft
> deep-navy #333D70 edge where needed. Overall mood matches a sky-blue-to-lavender pastel game
> (backdrop is #99C7FC→#E6D6FA, but the **sheet background must stay transparent**).
>
> High resolution, square canvas, sharp, centered, evenly spaced, transparent PNG.

### Negative / avoid
> no background, no drop-shadow onto a solid backdrop, no photorealism, no gradients-as-background,
> no text paragraphs, no watermark, no harsh black outlines, no metallic/realistic materials,
> no clutter, no overlapping items, consistent lighting (not per-item different light angles).

---

## Asset checklist (what the sheet must contain)

| Group | Items |
|---|---|
| Nav buttons (round) | Home · Retry · Undo · Settings/Gear · Close(X) · Pause · Play · Next |
| Item buttons (rounded square) | Sound-On · Sound-Off · **Hint (lightbulb)** · **Add-Time (clock +)** · Shuffle · Bomb · Magnet |
| Markers | Star (gold) · Star (empty) · Padlock · Splitter (◄►) · Door bar · count badge "xN" |
| Jelly blocks | red · blue · yellow · green · purple · orange · pink · lime |
| FX | sparkle · confetti burst · glow ring · "+15" pop |

## Style anchors (match the current game)
- **Glossy jelly/candy 3D** plastic look; rounded, chunky, friendly.
- Single **top-left light**, soft top highlight + soft lower shade, subtle rim light.
- **No hard outlines**; separation comes from shading + soft inner glow.
- Buttons read as pressable 3D candy (like the current Home/Retry/Undo circles).
- Padlock = **white** body with a soft dark rim so it stays visible on any block color.

## Palette (game-exact, hex)
Red `#F5383D` · Blue `#2185FF` · Yellow `#FFD417` · Green `#29CC5C` · Purple `#9947EB` ·
Orange `#FF871A` · Pink `#FF66B8` · Lime `#A8E61F` · Teal `#0DC7BD` · Brown `#8F5E38`
Background feel (do NOT paint it — keep transparent): sky `#99C7FC` → lavender `#E6D6FA`; text/navy `#333D70`.

## Technical requirements
- **Transparent background**, PNG-32 (RGBA), no baked backdrop.
- Square canvas, high-res (e.g. **2048×2048**), so each icon exports crisp.
- Even grid, generous padding, each item centered in its own cell, uniform scale & lighting.
- After generation, slice by the grid; the project's `AssetSetup.Run` already fixed-grid slices a
  sheet and alpha-trims each cell (see README / Editor tooling).

## Tips
- If the generator paints a background anyway, add: "isolated on pure transparent alpha, no
  backdrop, each icon separate" and export as PNG, or remove the bg afterward.
- Generate in 2 passes if one sheet gets crowded: (A) buttons + markers, (B) blocks + FX — keep
  the SAME style/lighting/palette prompt so they match.

# 64px Artwork Experiment — Reverted to 32px

The active renderer now uses the original 32×32 terrain and grass assets, plus the proportional 64×128 pine sprite. Foliage sizing is restored too. This change stays on `feature/64px-artwork-upgrade`; the branch name is retained. The unused 64px assets remain available for reference. World scale, background motion, and lighting are unchanged.

The following describes the earlier experiment, not the active artwork configuration.

This feature replaces every **currently used foreground art asset** with freshly generated, finer-grained pixel-grid artwork:

| Purpose | New file | Native dimensions |
| --- | --- | --- |
| Grass | assets/tiles/grass_top_64.svg | 64×64 |
| Dirt | assets/tiles/dirt_64.svg | 64×64 |
| Stone | assets/tiles/stone_64.svg | 64×64 |
| Grass and flowers | assets/foliage/grass_clump_64.svg | 64×64 |
| Pine tree | assets/foliage/pine_128x256.svg | 128×256 |

The pine sprite scales up proportionally from its earlier 64×128 format, enabling more branch and needle detail. The SVG assets have hard-edged, individually drawn color cells and are rendered with nearest-neighbor filtering.

**Important:** The world still uses 24×24 world-unit tiles for physics and generation. Source art resolution is independent of the game tile size. The existing procedural background remains adjustable in ForestBackdrop.cs; its procedural pixel step is not a texture asset and is unchanged.

## Test in Godot
1. Checkout feature/64px-artwork-upgrade and let Godot import the SVGs.
2. Run dotnet build and press F5.
3. Confirm grass, soil, stone, pine trees and foliage all render.
4. Check mining / placement still work and look at 100% zoom for sharp edges.
5. Check performance. These resources are intentionally more detailed; a texture atlas and chunk renderer are future optimizations.

## Lighting — upcoming separate feature
Introduce world lights, dark caves, and authored sun/campfire illumination **after** this artwork upgrade is verified. No lighting changes are bundled here.

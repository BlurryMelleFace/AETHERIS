# Forest Backdrop and Seasons — First Pass

## Current implementation
- `assets/backgrounds/moody_snowy_forest_96.png` is a **96×54 reduced-color pixel-art preview** of the original generated panorama. It was compressed for the first repository integration; it is intentionally less detailed than the concept image.
- `World.cs` draws the panorama behind the 32px foreground sprites and terrain. It repeats across the finite map, so seams can appear.
- `DayNightCycle.cs` is a single `CanvasModulate` that multiplies color over the entire canvas, including both the background and foreground.
- **Press T** to cycle spring/summer/autumn/winter tint. The transition is gradual and affects all world elements together.
- The existing day/night modulation is preserved.

## Future architecture
Move from global tint to an `EnvironmentState` resource containing time-of-day, season, weather, humidity and biome. Background, trees, ground, particles, ambient lights and sky each sample the same state but apply different response curves. This allows independent snow coverage, green/yellow leaves and weather effects without breaking visual coherence.

## Limitations
This currently changes **tone**, not tree topology, leaf textures, snow buildup or weather. Future seasonal systems should drive per-layer materials as well as the shared grade. Background repeat is a placeholder, not a seamless parallax-scrolling solution. The new image is a reduced-palette preview rather than a native-resolution panorama.

## Local test
Checkout `feature/32px-artwork-integration`, let Godot import the new PNG, `dotnet build`, press F5 and T to test seasonal tone changes. Check that both hills and player-world vegetation color-shift together.

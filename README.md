# AETHERIS

An atmospheric **2D survival sandbox RPG** with Terraria-style progression, Valheim-inspired atmosphere, Minecraft-like building, Subnautica-inspired oceans and Starbound-like planets.

## Current status
Pre-alpha **engineering prototype**. Contains a playable character, deterministic finite terrain, mining and block placement, and architectural/design documents. It is not yet chunk-streamed or optimized; visual art and assets are placeholders.

## Run locally
1. Install **Godot 4.4.1 .NET edition** and **.NET 8 SDK**.
2. Clone: `git clone https://github.com/BlurryMelleFace/AETHERIS.git`
3. Import/open `project.godot` in Godot .NET.
4. Build the C# solution and press F5.

Controls: **A/D or arrows** move, **Space** jumps, **left-click** mines, **right-click** places a dirt cell, **R** reloads the prototype world. Current prototype has no item inventory, reach limit, saving or player placement obstruction checks.

## Documentation
- [Game design](docs/GAME_DESIGN.md)
- [Technical architecture](docs/ARCHITECTURE.md)
- [Development roadmap](docs/ROADMAP.md)
- [Contributing](docs/CONTRIBUTING.md)

## Guiding principle
Build a polished forest and cave vertical slice before ocean simulation, planetary travel or multiplayer.

## License
All rights reserved until a project license is intentionally chosen. Do not add third-party assets without suitable permissions.

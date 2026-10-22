# AETHERIS — Technical Architecture

## Technology
Godot 4.4.1 .NET edition, C# (.NET 8). The 2D engine handles input, audio, physics, rendering and UI. Game systems should be modular and data-driven.

## Prototype (current)
- scenes/main.tscn wires Player and World.
- scripts/Player.cs provides placeholder movement and rendering.
- scripts/World.cs generates a deterministic, finite test landscape and lets the user mine/place cells.
- Collision currently uses individual StaticBody2D nodes: deliberately acceptable ONLY for a tiny prototype, not production scale.
- No persistent saves, chunk streaming, fluids, enemies, inventory, or networking yet.

## Target architecture
Domain/World: plain C# tile/chunk data, world seeds, biome and cave generation.
Presentation: chunk renderer, parallax scenery, lighting, weather, effects.
Simulation: mining, placement, fluids, ecosystem and time.
Gameplay: movement, combat, equipment, items, crafting, progression.
Persistence: versioned world metadata plus compressed changed chunks.
Planets: independent world IDs and seeds, player travel transitions.
Networking: authoritative multiplayer server only after single-player fundamentals stabilize.

## World scaling
Store tiles as data, not nodes. Divide terrain into chunks such as 64x64. Draw visible chunks using batched meshes, update only dirty chunks, generate off-thread when safe and commit scene changes on main thread. Separate collision and simulation radii.

## Engineering practices
Deterministic procedural generation; explicit save schema version; profile before optimizing; automated unit tests for seed reproducibility and chunk borders; content definitions externalized from core algorithms. Keep platform dependencies behind narrow adapters.

## Risks
Fluid simulation and arbitrary terrain edits are expensive. Plan isolated local simulation. Avoid network replication until state changes and serialization are stable.

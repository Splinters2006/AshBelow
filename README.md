# Ash / Below

A small Unity 6 action roguelike prototype, built with simple colored sprites. Explore connected procedural rooms, defeat every enemy, and find the gold stairs to choose an upgrade and descend. Death starts a new run; upgrades last only for the current run.

## Play

1. Open this folder with Unity **6000.6.3f1** through Unity Hub.
2. Open `Assets/Scenes/Dungeon.unity` and press **Play**. If the scene is missing, use **Slopgame → Create playable dungeon scene**.
3. Focus the Game view. Move with **WASD / arrow keys**, aim with the **mouse cursor**, hold **left mouse** for a cone-shaped sword slash, and press **E** at unlocked gold stairs.

Press **Space** to dodge in your movement direction (toward the cursor if standing still). The roll lasts 0.25 seconds, prevents damage during that window, and has a 0.8-second cooldown. You cannot attack during a roll, and walls still block movement. Sword attacks cover a 100-degree cone with a 1.65-unit reach and cannot hit through walls.

Red enemies chase and deal contact damage. Orange casters keep their distance and only damage you with projectiles, never by touch. They flash white for 0.45 seconds before firing toward your position at the start of the windup. Bolts stop at walls and disappear when changing floors or restarting.

Every floor must be cleared before descending. Choose damage, maximum health, or movement speed between floors. Enemies become stronger as you descend. After death, click **Begin a new run**.

## Structure

All C# lives in `Assets/Scripts`. `DungeonMap` generates connected rooms and handles wall collision. `DungeonRun` owns run progression and enemy navigation. Player, enemy, visuals, input, and HUD are separate components/helpers. The editor setup creates the scene through Unity APIs, and **Slopgame → Validate generated dungeons** checks connectivity and spawn positions across 500 seeds.

The prototype uses runtime-generated placeholder sprites and an IMGUI HUD, with no external art dependencies. It supports either Unity input backend. There is no save system, audio, inventory, or boss yet; the floors continue indefinitely.

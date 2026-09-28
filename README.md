# Ash / Below

A small Unity 6 action roguelike prototype, built with simple colored sprites. Explore connected procedural rooms, defeat every enemy, and find the gold stairs to choose an upgrade and descend. Death starts a new run; upgrades last only for the current run.

## Play

1. Open this folder with Unity **6000.6.3f1** through Unity Hub.
2. Open `Assets/Scenes/Dungeon.unity` and press **Play**. The main menu opens first; choose **Choose character → Knight or Archer → Begin run**. If the scene is missing, use **Slopgame → Create playable dungeon scene**.
3. Focus the Game view. Move with **WASD / arrow keys**, aim with the **mouse cursor**, hold **left mouse** to charge and release to attack (tap for a quick attack), and press **E** at unlocked gold stairs.

Press **Space** to dodge in your movement direction (toward the cursor if standing still). The roll lasts 0.25 seconds, prevents damage during that window, and has a 1.4-second cooldown. You cannot attack during a roll, and walls still block movement. Sword attacks have a 2.3-unit reach and cannot hit through walls. Charging for up to 1.2 seconds widens the cone from 60 to 120 degrees and raises damage from 1x to 3x your damage stat. Bow attacks fully charge in 1 second, also up to 3x damage. Damage increases in whole-HP steps; holding beyond full charge grants no extra damage. Movement slows while charging. Dodging cancels a charge.

Red enemies chase and deal contact damage. Orange casters keep their distance and only damage you with projectiles, never by touch. They flash white for 0.45 seconds before firing toward your position at the start of the windup. Bolts stop at walls and disappear when changing floors or restarting.

Every floor must be cleared before descending. Choose from three randomly offered boons between floors (fewer only if most boons have reached their stack caps). Every choice restores 2 HP. Enemies have 2 HP on floors 1-3 and gain 1 HP every floor afterward (3 HP on floor 4, 4 on floor 5, and so on). After death, click **Begin a new run**.

**Knight right click** raises a shield for **0.8 seconds** with a **2.8-second cooldown**. Its blue 120-degree arc faces the cursor direction when raised. Frontal projectiles reflect back along their incoming path and deal 2 damage to the first enemy they hit. The shield does not protect against rear projectiles or melee contact. Movement slows while blocking; left-click attacks are disabled. Dodging lowers it without refunding the cooldown. The HUD shows shield and dodge cooldowns and charge progress.

## Windows release (primary target)

Use **Slopgame → Windows → Build EXE and ZIP**. It builds Windows x64 using Mono and Direct3D 11, starting in a resizable 1280×720 window. Output goes to a new timestamped folder and ZIP under `Builds/Windows/`. Builds are excluded from Git.

The build requires Windows Build Support for the matching Unity editor. This Linux installation currently lacks that module; install Windows Build Support (Mono) or open the project with Unity 6000.6.3f1 on Windows. The menu reports missing support before starting a build. Windows execution still needs testing on a Windows PC.

Distribute the generated ZIP. Players extract it and launch `AshBelow.exe`, keeping its accompanying DLLs and data folders together. The ZIP includes launch instructions and controls. A single-file installer can be added later.

For automation, run Unity with `-batchmode -quit -projectPath <project-folder> -executeMethod Slopgame.Editor.WindowsBuild.Build -logFile <build-log>`.

## Structure

All C# lives in `Assets/Scripts`. `DungeonMap` generates connected rooms and handles wall collision. `DungeonRun` owns run progression and enemy navigation. Player, enemy, visuals, input, and HUD are separate components/helpers. The editor setup creates the scene through Unity APIs, and **Slopgame → Validate generated dungeons** checks connectivity and spawn positions across 500 seeds.

The prototype uses runtime-generated placeholder sprites and an IMGUI HUD, with no external art dependencies. It supports either Unity input backend. There is no save system, audio, inventory, or boss yet; the floors continue indefinitely.

## Characters and main menu

Knight and Archer are playable. Archer starts with **5 HP**, fires one arrow with left click, and fires **three arrows at -15°, 0°, +15°** with right click on a **6-second cooldown**. Tap-fired and triple-shot arrows deal the current damage stat; charged arrows deal up to 3x. All arrows travel up to 5 units, and stop at the first enemy or wall. Archer shares the dodge roll and upgrade system. The main menu offers character selection and Quit; use **Main menu** in the dungeon to abandon the current run and choose again. Starting a run resets upgrades and health.

Character definitions are ScriptableObjects in `Assets/Resources/Characters`. Create future definitions with **Assets → Create → Slopgame → Character** and give them unique names, descriptions, colors, and starting stats. The selector discovers them automatically. New weapons and abilities still need their own gameplay implementation; select Sword or Bow on the character asset to choose its existing weapon. Both share the dodge component.

## Powerups and enemy facing

Both classes can gain Keen Edge (+1 damage), Vitality (+2 max HP/full heal), Fleet Foot (+0.7 speed, 5 ranks), Quick Hands (+20% base attack speed, 5 ranks), Deadeye (+10% double-damage chance, 5 ranks), Ward (block one hit per floor per rank, 3 ranks), Soul Harvest (heal 1 HP every 5/4/3 kills, 3 ranks), and Second Wind (10% shorter dodge cooldown per rank, 3 ranks; minimum 0.98 seconds). Shield cooldown is 2.8 seconds and triple-shot cooldown is 6 seconds. Boons stack for the current run; capped boons stop appearing. The HUD lists acquired boons, critical chance, and remaining ward charges.

Enemies have a visible facing marker and turn at up to 180 degrees per second. Casters align before charging a shot and hold that facing during the windup. `EnemyFacing` identifies a 120-degree front sector, a 120-degree rear sector, and the sides. `DungeonEnemy.LastHitRegion` and `HitReceived` expose front/side/back hit information for future backstab abilities. Arrows use their incoming direction; melee uses the attacker's position. Rear hits currently receive no automatic bonus damage.

Class talents appear only for their matching class, with at least one available class talent in each floor offer. Knight gets Sweeping Edge (+15 degrees to the fully charged cone per rank, 2 ranks, maximum 150 degrees) and Riposte (+1 reflected bolt damage per rank, 3 ranks). Archer gets Quick Draw (15% shorter charge time per rank, 2 ranks) and Bodkin (+0.5x maximum charged damage per rank, 2 ranks, maximum 4x before critical hits). Talents reset with the run and never extend arrow range.

# Ash / Below

A small Unity 6 action roguelike prototype, built with simple colored sprites. Explore connected procedural rooms, defeat every enemy, and find the gold stairs to choose an upgrade and descend. Death starts a new run; upgrades last only for the current run.

## Play

1. Open this folder with Unity **6000.6.3f1** through Unity Hub.
2. Open `Assets/Scenes/Dungeon.unity` and press **Play**. The main menu opens first; choose **Choose your hero → class → Begin descent**. If the scene is missing, use **Slopgame → Create playable dungeon scene**.
3. Focus the Game view. Move with **WASD / arrow keys**, aim with the **mouse cursor**, hold **left mouse** to charge and release to attack (tap for a quick attack), and press **F** at unlocked gold stairs.

Press **Space** to dodge in your movement direction (toward the cursor if standing still). The roll lasts 0.25 seconds, prevents damage during that window, and has a 1.4-second cooldown. You cannot attack during a roll, and walls still block movement. Sword attacks have a 2.3-unit reach and cannot hit through walls. Charging for up to 1.2 seconds widens the cone from 60 to 120 degrees and raises damage from 1x to 3x your damage stat. Bow attacks fully charge in 1 second, also up to 3x damage. Damage increases in whole-HP steps; holding beyond full charge grants no extra damage. Movement slows while charging. Dodging cancels a charge.

Red ashling enemies chase and deal contact damage. Melee packs spread around the player while the closest enemy presses the attack. Armored iron brutes appear in alternating rooms, with three times normal HP, 60% normal movement speed, a larger body, and reduced knockback. Orange hooded casters steer around walls and keep their distance and only damage you with projectiles, never by touch. They flash white for 0.45 seconds before firing toward your position at the start of the windup. Bolts stop at walls and disappear when changing floors or restarting.

Every floor must be cleared before descending. Choose from three randomly offered boons between floors (fewer only if most boons have reached their stack caps). Every choice restores 2 HP. Enemies have 2 HP on floors 1-3 and gain 1 HP every floor afterward (3 HP on floor 4, 4 on floor 5, and so on). After death, click **Begin a new run**.

**Knight right click** raises a shield for **0.8 seconds** with a **2.8-second cooldown**. Its blue 120-degree arc faces the cursor direction when raised. Frontal projectiles reflect back along their incoming path and deal 2 damage to the first enemy they hit. The shield does not protect against rear projectiles or melee contact. Movement slows while blocking; left-click attacks are disabled. Dodging lowers it without refunding the cooldown. The HUD shows shield and dodge cooldowns and charge progress.

## Windows release (primary target)

Use **Slopgame → Windows → Build EXE and ZIP**. It builds Windows x64 using Mono and Direct3D 11, starting in a resizable 1280×720 window. Output goes to a new timestamped folder and ZIP under `Builds/Windows/`. Builds are excluded from Git.

The build requires Windows Build Support for the matching Unity editor. This Linux installation currently lacks that module; install Windows Build Support (Mono) or open the project with Unity 6000.6.3f1 on Windows. The menu reports missing support before starting a build. Windows execution still needs testing on a Windows PC.

Distribute the generated ZIP. Players extract it and launch `AshBelow.exe`, keeping its accompanying DLLs and data folders together. The ZIP includes launch instructions and controls. A single-file installer can be added later.

For automation, run Unity with `-batchmode -quit -projectPath <project-folder> -executeMethod Slopgame.Editor.WindowsBuild.Build -logFile <build-log>`.

## Structure

All C# lives in `Assets/Scripts`. `DungeonMap` generates connected rooms and handles wall collision. `DungeonRun` owns run progression and enemy navigation. Player, enemy, visuals, input, and HUD are separate components/helpers. The editor setup creates the scene through Unity APIs, and **Slopgame → Validate generated dungeons** checks connectivity and spawn positions across 500 seeds.

The prototype uses runtime-generated placeholder sprites and an IMGUI HUD, with no external art dependencies. The project uses Unity Input System 1.20.0. Restart the editor if Unity requests it after importing the package. There is no save system, audio, or general inventory yet; the floors continue indefinitely. Boss arenas appear every fifth floor.

## Characters and main menu

Knight, Archer, Wizard, Assassin, and Paladin are playable. Archer starts with **5 HP**, fires one arrow with left click, and fires **three arrows at -15°, 0°, +15°** with right click on a **6-second cooldown**. Tap-fired and triple-shot arrows deal the current damage stat; charged arrows deal up to 3x. Basic arrows travel 5 units when tapped, 5.5 at half charge, and 6 at full charge. Holding beyond full charge grants no extra range. Triple shots and relic arrows keep their 5-unit range. Arrows stop at the first enemy or wall, except Piercing Shot. Archer shares the dodge roll and upgrade system. The main menu offers character selection and Quit; use **Main menu** in the dungeon to abandon the current run and choose again. Starting a run resets upgrades and health.

Character definitions are ScriptableObjects in `Assets/Resources/Characters`. Create future definitions with **Assets → Create → Slopgame → Character** and give them unique names, descriptions, colors, and starting stats. The selector discovers them automatically. New weapons and abilities still need their own gameplay implementation; select Sword, Bow, Staff, Daggers, or Hammer on the character asset to choose its weapon. All classes share the dodge component.

## Powerups and enemy facing

All classes can gain Keen Edge (+1 damage), Vitality (+2 max HP/full heal), Fleet Foot (+0.7 speed, 5 ranks), Quick Hands (+20% base attack and charge speed, 5 ranks), Precision (+10% physical critical / elemental effect chance, 5 ranks), Ward (block one hit per floor per rank, 3 ranks), Soul Harvest (heal 1 HP every 5/4/3 kills, 3 ranks), and Second Wind (10% shorter dodge cooldown per rank, 3 ranks; minimum 0.98 seconds). Shield cooldown is 2.8 seconds and triple-shot cooldown is 6 seconds. Boons stack for the current run; capped boons stop appearing. The HUD lists acquired boons, critical chance, and remaining ward charges.

Enemies have a visible facing marker and turn at up to 180 degrees per second, slowing to 63 degrees per second within 0.75 units. Casters align before charging a shot and hold that facing during the windup. `EnemyFacing` identifies a 120-degree front sector, a 120-degree rear sector, and the sides. `DungeonEnemy.LastHitRegion` and `HitReceived` expose front/side/back hit information for future backstab abilities. Arrows use their incoming direction; melee uses the attacker's position. Assassin physical rear hits deal double base damage before critical hits and gain another +1 per Hidden Blade rank.

Class talents appear only for their matching class, with at least one available class talent in each floor offer. Knight gets Sweeping Edge (+15 degrees to the fully charged cone per rank, 2 ranks, maximum 150 degrees) and Riposte (+1 reflected bolt damage per rank, 3 ranks). Archer gets Quick Draw (15% shorter charge time per rank, 2 ranks) and Bodkin (+0.5x maximum charged damage per rank, 2 ranks, maximum 4x before critical hits). Talents reset with the run and never extend arrow range.

## Boss arenas and artifacts

Floors **5, 10, 15, ...** replace the normal dungeon with a dedicated arena and a guardian. Guardians have **24 + 3 × floor HP**, telegraph their fan and radial attacks, and become more aggressive below half health. Defeating a guardian clears its remaining hostile bolts and drops a glowing artifact. Walk up and press **F** to claim it; claim or leave the artifact before descending.

Boss artifacts offer **active abilities for the selected class instead of a passive reward**. Choose **Q** or **E** for every new ability, including your first artifact. You can equip only two. Later artifacts let you upgrade an equipped ability (up to rank 3) or explicitly replace Q or E. Replacing a slot preserves its current cooldown. Ordinary floors still offer passive talents, including upgrades for your currently equipped abilities. Ability talents cannot appear before you unlock/equip that ability. All unlocks reset on a new run.

| Class | Left click / right click | Boss artifact choices |
| --- | --- | --- |
| Knight | Charged sweeping slash / reflecting shield | Shield Rush, Earthshatter, Aegis |
| Archer | Charged 5–6-unit arrow / triple shot | Arrow Volley, Piercing Shot, Windstep |
| Wizard | Charged fireball / lightning (3s) | Inferno Orb, Frost Nova, Arcane Blink |
| Assassin | Charged daggers / Shadowstep (4s) | Fan of Knives, Venom Strike, Shadow Veil |
| Paladin | Charged hammer / reflecting shield | Healing Light, Judgment, Sanctuary |

Wizard starts with 4 HP. Lightning reaches 6 units and hits one enemy by default. Conductivity unlocks one additional chain target per rank within 2.5 units of the previous target; walls block each jump and no target is hit twice in a cast. Storm Reach increases cast/jump range, High Voltage increases damage, and Conductivity adds chain targets. Left-click fireballs have a 6-unit range; Inferno Orb is the boss-unlocked explosive version. Fire and ice talents become available after their corresponding artifact is equipped.

Assassin starts with 4 HP, 5.6 movement speed, and **15% physical crit chance**. Paladin starts with 7 HP and 4.5 movement speed. Healing Light and Sanctuary affect the caster and any friendly DungeonPlayer within 4 units in the same run; the current game remains single-player.

## Physical and elemental effects

Every class begins with **5% physical critical / elemental effect chance**. Physical critical hits deal double damage. Assassin adds 10% physical crit chance and can improve it further through Killer Instinct. Elemental damage **never critically strikes**: fire can ignite for three damage-over-time ticks with a pulsing flame indicator (green for venom), ice can chill, slowing enemy movement, turning, attack windups, cooldowns, and repeated contact attacks by 50%, and lightning's effect roll overloads a Conductivity-unlocked chain with two extra short jumps. Precision improves both critical and elemental effect chances. Bosses have half-duration chill and resist knockback.

The interface includes an animated health bar, a boss health bar and attack warning, Q/E cooldown slots, charge feedback, a scrollable build panel, and class-colored reward cards. Menus use a centered 1280×720 reference layout and scale to the window. Reward choices pause the game and its cooldown/status timers.

## Validation

Run the original combat regression using `Slopgame.Editor.DungeonProjectSetup.SmokeTest` in a disposable Unity project copy. Run `Slopgame.Editor.BossArtifactTests.Run` for boss floors, all five classes, Q/E slots, all 15 active abilities, element/crit separation, talent gates, cooldowns, and reset. `Slopgame.Editor.UiPreviewCapture.Capture` captures menu, class, arena, artifact, and talent screens with a graphical Unity editor. These automated commands exit the editor when complete; do not invoke them in an unsaved interactive editor session.

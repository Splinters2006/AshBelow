# Ash / Below

A small Unity 6 action roguelike prototype with procedural sprites and spell effects. Explore connected rooms, defeat every enemy, and find the gold stairs to choose a boon and descend. Death starts a new run; Ash and purchased shop upgrades carry over, while floor talents and artifact abilities reset.

## Play

1. Open this folder with Unity **6000.6.3f1** through Unity Hub.
2. Open `Assets/Scenes/Dungeon.unity` and press **Play**. The main menu opens first; choose **Choose your hero → class → Begin descent**. If the scene is missing, use **Slopgame → Create playable dungeon scene**.
3. Focus the Game view. Move with **WASD / arrow keys**, aim with the **mouse cursor**, hold **left mouse** to charge and release to attack (tap for a quick attack), and press **F** at unlocked gold stairs.

Press **Space** to dodge in your movement direction (toward the cursor if standing still). The roll lasts 0.25 seconds, prevents damage during that window, and has a 1.4-second cooldown. You cannot attack during a roll, and walls still block movement. Sword attacks have a 2.3-unit reach and cannot hit through walls. Knight charging for up to 1.2 seconds widens the cone from 60 to 120 degrees and raises damage from 1x to 3x your damage stat. Bow attacks fully charge in 1 second, also up to 3x damage. Damage increases in whole-HP steps; holding beyond full charge grants no extra damage. Movement slows while charging. Dodging cancels a charge.

Red ashling enemies chase and deal contact damage. Melee packs spread around the player while the closest enemy presses the attack. Armored iron brutes appear in alternating rooms, with three times normal HP, 60% normal movement speed, a larger body, and reduced knockback. Orange hooded casters steer around walls and keep their distance and only damage you with projectiles, never by touch. They flash white for 0.45 seconds before firing toward your position at the start of the windup. Bolts stop at walls and disappear when changing floors or restarting.

Every floor must be cleared before descending. Choose from three randomly offered boons between floors (fewer only if most boons have reached their stack caps). Every choice restores 2 HP. Enemies have 2 HP on floors 1-3 and gain 1 HP every floor afterward (3 HP on floor 4, 4 on floor 5, and so on). After death, click **Begin a new run**.

**Knight right click** raises a shield for **0.8 seconds** with a **2.8-second cooldown**. Its blue 120-degree arc faces the cursor direction when raised. Frontal projectiles reflect back along their incoming path and deal 2 damage to the first enemy they hit. The shield does not protect against rear projectiles or melee contact. Movement slows while blocking; left-click attacks are disabled. Dodging lowers it without refunding the cooldown. The HUD shows shield and dodge cooldowns and charge progress.

## Debug admin mode

Press **F1** at any time (or use **Debug admin mode** on the main menu) to toggle a session-only debug mode for testing. While it is on, the player cannot lose HP, every hit kills its target outright, dodge, class-skill (RMB) and Q/E relic cooldowns are skipped, and movement speed is doubled. A pink banner at the top of the HUD shows it is active. In a solo run, a **Skip room** button next to Talents jumps straight to the next floor, boss arenas included. It works with every class, resets when the game restarts, and never changes the save file.

## Combat effects and aiming

Every hero has a soft class-coloured ground ring, leaves tinted afterimages while dodging, and gets sweeping slash crescents, impact sparks (bigger with a gold flash on critical hits), projectile trails, glowing lightning, charge-ready pings, shield/reflect flashes, and blessing/healing motes. These effects are intentionally calmer than the Admin's shadow magic, which keeps its own effect set. Archer shows a faint aim line with an end marker at exactly where a basic arrow will land; it stretches and brightens while charging (a ghost tick marks the full-charge range) and turns red where a wall will stop the arrow.

## Co-op multiplayer

Up to four players can descend together. Choose **Co-op** on the main menu:

- **Host a party** creates an online party and shows a short **join code**. Friends type it under **Join code**. This goes through Unity Relay, so nobody needs to forward ports.
- **Host P2P / Join IP** connects players straight to the host over UDP port 7777, with no online service or account. When hosting, the game asks the router to open that port automatically (**UPnP**, or **NAT-PMP** on Apple and some other routers), then shows a **share address** such as `203.0.113.7:7777` with a **Copy** button, plus the same-network address. Friends paste it into the address field and press **Join IP**; the field accepts `ip`, `ip:port`, `hostname:port` and `[ipv6]:port`. The mapping is renewed while hosting and removed when the party closes.
  - If the router refuses (UPnP/NAT-PMP switched off), the lobby says so: same-network friends can still join, and internet friends need UDP 7777 forwarded to the host PC by hand, or the online join code instead.
  - If the router sits behind another router or the internet provider's shared address (CGNAT), direct P2P cannot be reached from outside and the lobby recommends **Host a party** instead.
  - When the router does not report a public address, the host's game asks `api.ipify.org` for it so it can show a share address.
  - On Windows, allow **AshBelow** through the firewall when Windows asks the first time you host.

Everyone picks a hero in the party lobby, then the host presses **Begin descent**. Every player sees the same floors, since they are built from a shared seed. Each player controls their own hero, and the host's game runs the enemies. Enemy health grows by half for each extra hero. Any player can open the stairs or claim a boss artifact once the floor is clear. Everyone then picks their own boon or relic, and the party moves on when all have chosen. The game does not pause in co-op. A fallen hero watches a teammate and rises with half health on the next floor, and the run ends when the whole party has fallen. Healing Light, Sanctuary and the Paladin's blessing also reach teammates. Every player earns kill and floor Ash into their own save. If the host leaves, everyone returns to the menu; a guest who leaves simply disappears from the run. Parties cannot be joined once a descent has started.

**Online setup (one time, project owner):** Unity Relay only works in builds from a project linked to Unity Cloud. In Unity, open **Edit → Project Settings → Services**, choose your organization and **Create/Link** a project, then rebuild. Until then, **Host a party** explains that online play is not set up, and direct P2P still works.

**Automated check:** `Slopgame.Editor.CoopTests.Run` covers messages, the shared enemy pathing, health scaling, seed determinism and P2P address parsing. `Slopgame.Editor.CoopTests.BuildSmokePlayer` builds a Linux development player to `Builds/CoopSmoke`. Run it once with `-batchmode -nographics -coopSmoke host -coopPeers N` and N-1 more times with `-coopSmoke join -coopPeers N` to play a scripted 2–4 player descent over `127.0.0.1`. Each copy logs `COOP_SMOKE_<ROLE>_OK`, and smoke runs use a throwaway save.

## Windows release (primary target)

Use **Slopgame → Windows → Build EXE and ZIP**. It builds Windows x64 using Mono and Direct3D 11, starting in a resizable 1280×720 window. Output goes to a new timestamped folder and ZIP under `Builds/Windows/`. Builds are excluded from Git.

The build requires Windows Build Support (Mono) for the matching Unity editor. The menu reports missing support before starting a build.

Distribute the generated ZIP. Players extract it and launch `AshBelow.exe`, keeping its accompanying DLLs and data folders together. The ZIP includes launch instructions and controls. A single-file installer can be added later.

For automation, run Unity with `-batchmode -quit -projectPath <project-folder> -executeMethod Slopgame.Editor.WindowsBuild.Build -logFile <build-log>`.

## Structure

All C# lives in `Assets/Scripts`. `DungeonMap` generates connected rooms and handles wall collision. `DungeonRun` owns run progression and enemy navigation. Player, enemy, visuals, input, and HUD are separate components/helpers. The editor setup creates the scene through Unity APIs, and **Slopgame → Validate generated dungeons** checks connectivity and spawn positions across 500 seeds.

The prototype uses runtime-generated sprites and an IMGUI HUD, with no external art dependencies. The project uses Unity Input System 1.20.0. Restart the editor if Unity requests it after importing the package. There is no audio or general inventory yet; the floors continue indefinitely. Boss arenas appear every fifth floor.

## Ash, permanent upgrades, and updates

Each ordinary enemy pays **1 Ash**, each guardian pays **50 Ash**, and clearing any floor pays another **10 Ash** immediately. Rewards cannot be claimed twice. The main-menu **Ash shop** offers permanent health, damage, movement, attack/charge speed, and dodge upgrades for all heroes, plus upgrades specific to Knight, Archer, Wizard, Assassin, Paladin, and Brawler. Purchases apply when your next run starts; normal run talents retain their own stack limits.

Ash and purchases are saved after every reward or purchase, outside the installation folder, at `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Ash Below\progress.json` on Windows. Writes are atomic, the previous save is retained as `.bak`, and a damaged primary save can recover from that backup. A purchase takes effect only after it is saved successfully. Keep the company/product names stable when building, because Unity uses them to locate this folder.

Windows ZIPs include **Update.cmd**, `update-game.py` and `install-python.ps1`. To update, close the game, run Update.cmd, then launch the new version at the printed path. Update.cmd uses an installed Python 3.10+ if there is one. If there isn't, it downloads the official portable Python once from python.org into `%LOCALAPPDATA%\AshBelow\python`. The download is checked against a pinned SHA-256 checksum, needs no admin rights, and does not change PATH or anything else on the PC. Delete that folder to remove it. The updater fetches the latest published GitHub release ZIP, backs up your save folder, and places the build beside the old installation so you can roll back. It does not replace or reset your Ash. A published Windows build ZIP is required; a GitHub source archive is not a playable release.

For a source checkout, run `python scripts/update-game.py --mode source --directory .`. Source updates require a clean `main` branch and use a fast-forward merge, preserving local work by stopping when changes or divergent history exist. Add `--check` to either mode to check availability without installing. The updater refuses any installation or checkout that overlaps the player save folder.

## Characters and main menu

Knight, Archer, Wizard, Assassin, Paladin, Brawler, and Admin are playable. Archer starts with **5 HP**, fires one arrow with left click, and fires **three arrows at -15°, 0°, +15°** with right click on a **6-second cooldown**. Tap-fired and triple-shot arrows deal the current damage stat; charged arrows deal up to 3x. Basic arrows travel 5 units when tapped, 5.5 at half charge, and 6 at full charge. Holding beyond full charge grants no extra range. Triple shots and relic arrows keep their 5-unit range. Arrows stop at the first enemy or wall, except Piercing Shot. Archer shares the dodge roll and upgrade system. The main menu offers character selection, the Ash shop, and Quit; use **Main menu** in the dungeon to abandon the current run and choose again. Starting a run resets floor talents and abilities, then applies permanent purchases and restores health.

Character definitions are ScriptableObjects in `Assets/Resources/Characters`. Create future definitions with **Assets → Create → Slopgame → Character** and give them unique names, descriptions, colors, and starting stats. The selector discovers them automatically. New weapons and abilities still need their own gameplay implementation; select Sword, Bow, Staff, Daggers, Hammer, or Shadow on the character asset to choose its moveset. The existing Hammer option now selects the Paladin sword-and-blessing moveset. All classes share the dodge component.

**Admin** is an intentionally overpowered shadow caster with **12 HP**, **32 base damage**, and **6.4 movement speed** before permanent upgrades. Left click tears shadow rifts through enemies; right-click **Nightfall** instantly executes visible enemies, including guardians, within 9 units. Its dark aura, violet rifts, soul bursts, and execution effects emphasize excessive power. Boss artifacts unlock **Eclipse**, **Soul Rend**, or **Shadow Reign** in your chosen Q/E slot, with dedicated talents for the class and its equipped abilities. Shared permanent shop upgrades apply to Admin too.

## Powerups and enemy facing

All classes can gain Keen Edge (+1 damage), Vitality (+2 max HP/full heal), Fleet Foot (+0.7 speed, 5 ranks), Quick Hands (+20% base attack and charge speed, 5 ranks), Precision (+10% physical critical / elemental effect chance, 5 ranks), Ward (block one hit per floor per rank, 3 ranks), Soul Harvest (heal 1 HP every 5/4/3 kills, 3 ranks), and Second Wind (10% shorter dodge cooldown per rank, 3 ranks; minimum 0.98 seconds). Shield cooldown is 2.8 seconds and triple-shot cooldown is 6 seconds. Boons stack for the current run; capped boons stop appearing. The HUD lists acquired boons, critical chance, and remaining ward charges.

Enemies have a visible facing marker and turn at up to 180 degrees per second, slowing to 63 degrees per second within 0.75 units. Casters align before charging a shot and hold that facing during the windup. `EnemyFacing` identifies a 120-degree front sector, a 120-degree rear sector, and the sides. `DungeonEnemy.LastHitRegion` and `HitReceived` expose front/side/back hit information for future backstab abilities. Arrows use their incoming direction; melee uses the attacker's position. Assassin physical rear hits deal double base damage before critical hits and gain another +1 per Hidden Blade rank.

Class talents appear only for their matching class, with at least one available class talent in each floor offer. Knight gets Sweeping Edge (+15 degrees to the fully charged cone per rank, 2 ranks, maximum 150 degrees) and Riposte (+1 reflected bolt damage per rank, 3 ranks). Archer gets Quick Draw (15% shorter charge time per rank, 2 ranks) and Bodkin (+0.5x maximum charged damage per rank, 2 ranks, maximum 4x before critical hits). Talents reset with the run and never extend arrow range.

## Boss arenas and artifacts

Floors **5, 10, 15, ...** replace the normal dungeon with a dedicated arena and a guardian. Three guardians take turns (floor 5, 10, 15, then again from floor 20). Each telegraphs its attacks and gets more aggressive below half health:

- **The Ash Warden** (**24 + 3 × floor HP**) alternates an aimed ember fan and a radial nova.
- **The Ashen Duelist** (**12 + 2 × floor HP**) is fragile but very agile. It circles the party, chains three dash strikes (four when bloodied) along a flashing line, and sometimes sidesteps out of combos when hit. It is winded after each chain.
- **Malphas, the Hellfire Archdemon** (**30 + 4 × floor HP**) rotates between three attacks. **Infernal Cross** sends out pillars of fire, and **Brimstone Rain** drops meteors that leave burning craters. **Hellfire Nova** sends out expanding walls of fire that you must roll through. His signature is **Cataclysm**. The whole arena erupts in hellfire except one small golden circle. He flies overhead, invulnerable, and fires bolts down at you, then crashes back down and is staggered for a few seconds. He casts it right away the first time he drops below half health. Defeating a guardian clears its remaining hostile bolts and drops a glowing artifact. Walk up and press **F** to claim it; claim or leave the artifact before descending.

Boss artifacts offer **active abilities for the selected class instead of a passive reward**. Choose **Q** or **E** for every new ability, including your first artifact. You can equip only two. Later artifacts let you upgrade an equipped ability (up to rank 3) or explicitly replace Q or E. Replacing a slot preserves its current cooldown. Ordinary floors still offer passive talents, including upgrades for your currently equipped abilities. Ability talents cannot appear before you unlock/equip that ability. All unlocks reset on a new run.

| Class | Left click / right click | Boss artifact choices |
| --- | --- | --- |
| Knight | Charged sweeping slash / reflecting shield | Shield Rush, Earthshatter, Aegis |
| Archer | Charged 5–6-unit arrow / triple shot | Arrow Volley, Piercing Shot, Windstep |
| Wizard | Charged fireball / lightning (3s) | Inferno Orb, Frost Nova, Arcane Blink |
| Assassin | Charged daggers / Shadowstep (4s) | Fan of Knives, Venom Strike, Shadow Veil |
| Paladin | Weak sword swipe / reflecting shield | Healing Light, Judgment, Sanctuary |
| Admin | Charged shadow rifts / Nightfall execution | Eclipse, Soul Rend, Shadow Reign |
| Brawler | Fast jab / charged punch barrage / Empower (10s) | Knuckle Sandwich, Wild Leap, Primal Rage |

Wizard starts with 4 HP. Lightning reaches 6 units and hits one enemy by default. Conductivity unlocks one additional chain target per rank within 2.5 units of the previous target; walls block each jump and no target is hit twice in a cast. Storm Reach increases cast/jump range, High Voltage increases damage, and Conductivity adds chain targets. Left-click fireballs have a 6-unit range; Inferno Orb is the boss-unlocked explosive version. Fire and ice talents become available after their corresponding artifact is equipped.

Assassin starts with 4 HP, 5.6 movement speed, and **15% physical crit chance**. Paladin starts with 7 HP and 4.5 movement speed. Tap left click for a short, weak sword swipe (half base damage, minimum 1, plus any blessing bonus). Hold left click for 3 seconds, then release to bless yourself and friendly players within 4 units with +2 damage for 8 seconds. Attack speed shortens this charge. Releasing early performs only the weak swipe; dodging or raising the shield cancels charging. A gold circle previews the buff radius and the HUD shows its remaining duration. Recasting refreshes the buff without stacking its damage. Healing Light and Sanctuary affect the caster and any friendly DungeonPlayer within 4 units in the same run; the current game remains single-player.

Brawler is a puppygirl pit-fighter with floppy ears, a tail and big red gloves: 6 HP and 5.3 movement speed. Tap left click for a fast jab that hits every enemy in a short rectangle ahead. Holding charges slowly (1.8 seconds) and a full release unleashes a **barrage of 6 punches** in a bigger rectangle; light hits keep enemies in place and the last punch hits twice as hard and knocks them back. A rectangle previews the reach while charging. Right-click **Empower** lasts 5 seconds on a 10-second cooldown: faster movement, attacks and charging, slightly larger punches, and 0.5 seconds off the dodge cooldown. **Knuckle Sandwich** throws one massive punch in a big rectangle (ranks grow its size and damage). **Wild Leap** pounces onto the best enemy within 7 units toward your aim, invulnerable in the air, and slams down for heavy area damage (16-second cooldown). **Primal Rage** doubles damage, halves charge time and dodge cooldown, and speeds up movement and dodges for 10 seconds, then leaves the Brawler **tired** for 5 seconds with all of those turned into penalties; its 25-second cooldown only starts once the tiredness ends. The HUD shows active buffs, and teammates see the Empower/Rage/Tired tints in co-op.

**Rear hits** are marked for every class: a pink double chevron behind the enemy, a ring and a spark burst out of its front. Teammates see the marker too.

## Physical and elemental effects

Assassin charges **50% faster** (0.8 seconds before upgrades). Charging narrows its cone from **60 to 22 degrees** while increasing damage up to **5x**. Right-click Shadowstep blinks up to 3 units through obstacles or walls, falling back to the farthest safe landing when the destination is blocked. Each enemy crossed takes one fully charged backstab with **twice the Assassin's physical critical chance**, capped at 100%. A failed blink consumes no cooldown; a successful one grants brief protection and starts the 4-second cooldown.

Every class begins with **5% physical critical / elemental effect chance**. Physical critical hits deal double damage. Assassin adds 10% physical crit chance and can improve it further through Killer Instinct. Elemental damage **never critically strikes**: fire can ignite for three damage-over-time ticks with a pulsing flame indicator (green for venom), ice can chill, slowing enemy movement, turning, attack windups, cooldowns, and repeated contact attacks by 50%, and lightning's effect roll overloads a Conductivity-unlocked chain with two extra short jumps. Precision improves both critical and elemental effect chances. Bosses have half-duration chill and resist knockback.

The interface includes an animated health bar, a boss health bar and attack warning, Q/E cooldown slots, charge feedback, a scrollable build panel, and class-colored reward cards. Menus use a centered 1280×720 reference layout and scale to the window. Reward choices pause the game and its cooldown/status timers.

## Validation

Run the original combat regression using `Slopgame.Editor.DungeonProjectSetup.SmokeTest` in a disposable Unity project copy. Run `Slopgame.Editor.BossArtifactTests.Run` for boss floors, class abilities, Q/E slots, element/crit separation, talent gates, cooldowns, and reset. Run `Slopgame.Editor.BossRosterTests.Run` for the Warden/Duelist/Archdemon rotation, Duelist dashes, the Archdemon's Cataclysm and invulnerable flight, and the debug skip-room button. Run `Slopgame.Editor.AdminTests.Run` and `Slopgame.Editor.AssassinTests.Run` in batch mode for shadow attacks, capped charges, wall crossing, critical backstabs, and VFX cleanup. Run `Slopgame.Editor.ProgressionTests.Run` in batch mode for Ash rewards, permanent upgrades across every class, backup recovery, failed writes, and save survival. Batch tests use temporary wallets rather than the player's real save. Run `python -B -m unittest discover -s tests -p test_updater.py -v` for offline update/install, save preservation, and failure tests. `Slopgame.Editor.UiPreviewCapture.Capture` captures menu, class, arena, artifact, and talent screens with a graphical Unity editor. `Slopgame.Editor.AdminPreviewCapture.Capture` captures Admin selection, Nightfall, and artifact screens. Both preview tools use temporary saves. These automated Unity commands exit the editor when complete; do not invoke them in an unsaved interactive editor session.

# Ash / Below

An action roguelike. Pick a hero, fight through connected dungeon rooms, beat a guardian every five floors, and see how deep you can go. Play alone or with up to three friends.

## Install and play

1. Download the latest Windows release ZIP.
2. Extract it anywhere and run **`AshBelow.exe`**. Keep the other files and folders from the ZIP next to it.
   On Linux, download the `AshBelow-Linux-*.zip` release instead and run **`./AshBelow.x86_64`** (after `chmod +x AshBelow.x86_64` the first time).
3. In the main menu, pick **Choose your hero → a class → Begin descent**.

The game starts in a resizable 1280×720 window.

## Controls

| Action | Default |
| --- | --- |
| Move | **WASD** or **arrow keys** |
| Aim | Mouse cursor |
| Attack | **Left click**. Tap for a quick attack, hold to charge and release for a stronger one. |
| Class skill | **Right click** |
| Dodge roll | **Space** (you can't be hit during the roll, and you can steer it with the movement keys) |
| Relic abilities | **Q** and **E** |
| Class mechanic | **R** (bought in the Ash shop; the Gambler's purse is free) |
| Use stairs / claim artifact | **F** |

You can rebind every action under **Controls** in the main menu. Each action gets a main and an alternate key or mouse button. The HUD always shows your current keys.

## How a run works

- **Clear the floor.** Defeat every enemy, then find the gold stairs and press **F** to descend.
- **Pick a boon.** Between floors you choose one of three upgrades for this run. Each choice also restores 2 HP. Some boons belong to your class, and some only show up after you unlock the ability they improve. Hybrid talents change how one of your abilities behaves and only show up once you've learned that ability. Open **Build** in the HUD to see your talents.
- **Gather crystals.** Every enemy you defeat drops purple crystals (armoured brutes drop 3, guardians 15). Walk near them to pick them up. They last the whole run.
- **Visit the crystal shop.** Before every guardian you reach a lantern-lit shop. The merchant glows when you can talk to him. Press **F** at his counter to spend crystals. Each shop stocks a random five wares:
  - **One healing ware:** a Healing Draught (+2 HP) or a Grand Elixir (full HP).
  - **Two boss-fight boons,** which last only for the next guardian: Stoneskin Tonic (+2 wards), Whetstone (+1 damage) or Quicksilver (+20% move speed).
  - **Two relics,** which last the rest of the run: Heart Crystal (+1 max HP), Ember Hone (+1 damage), Windrunner Boots (+0.7 speed), Quickfinger Gloves (+20% attack speed), Hawkeye Lens (+10% crit chance), Warding Sigil (+1 ward every floor), Vampire Fang (a rank of Soul Harvest) or Phoenix Feather (shorter dodge cooldown). Relics add ranks to the matching boon and stop selling once it's maxed.

  Each ware costs 50% more every time you buy it in the same shop. In co-op, everyone sees the same stock. The shop's stairs lead to the guardian.
- **Beat the guardians.** Every fifth floor is a boss arena. Each world has three guardians. They are aggressive: they close in fast, attack quickly, and get more dangerous below half health:
  - **The Rime Warden**, a frost caster who fills the arena with icicle fans, frost novas, blizzard spirals, hailstorms, closing rings of ice, lancing frost beams, sweeping walls of frost and hailstones that shatter into icicles.
  - **The Steel Duelist**, a fast, fragile swordsman who dashes, throws blade fans, teleports behind you and cuts crosses of blade light.
  - **Malphas, the Hellfire Archdemon**. Watch for **Cataclysm**: the floor turns red, then everything except one golden circle erupts in fire. A gold arrow at your feet points to the circle.
  - **Grid Overseer** (Neon Arcology, floor 20): cyan scan grids, target traces and sequential firewalls. Step between the lanes.
  - **Bastion, the Siege Engine** (floor 25): amber missile walks, cluster mines and twin rail broadsides. Clear the reticles or shelter between the rails. Between volleys he stamps the ground: an earthquake cracks open a band of the arena (two when he's enraged) and seals it off for a few seconds. Get off the cracking ground before it breaks.
  - **Null, the Singularity Core** (floor 30): violet spiral streams, satellite crossfire and repeated orbital pulse waves. Weave between bolts and dodge each wave.
  - **The Hex Matriarch** (Infernal Court, floor 35): rings of hex bolts, a burning pentagram around you (leave the star), and a blink behind you followed by a fan of bolts.
  - **Gorgoth, the Brimstone Hound** (floor 40): lane charges that leave burning pawprints, a sweeping fan of molten breath, and eruptions of shockwaves and falling brimstone.
  - **Vassago, the Infernal Judge** (floor 45): a grid of hellfire chains (find the open square), a double spiral of hex bolts, and a gavel that slams wherever you stand.
  The Arcology guardians vent between attacks, giving you time to punish them. The Infernal Court's guardians tire the same way, summon imps, hellhounds and cultists every few attacks (their minions dissolve when they fall), and their brimstone and hex bolts set you alight: you keep burning until the flames gutter out or you dodge-roll to put them out.
- **Claim an artifact.** A beaten guardian drops an artifact that offers three abilities from your hero's pool (four with Sanctified Relics). Pick one to learn, or raise one you already know (up to rank 3). The abilities page then lists every ability you've learned this run so you can choose which sit on **Q** and **E**; it's also the **Abilities** tab of the **Build** panel, so you can swap any time (cooldowns stay with each ability). You can also leave an artifact for 40 crystals.
- **Death ends the run.** Boons and abilities reset, but your **Ash** and Ash shop upgrades are kept.

Enemies get tougher every floor. **The Infernal Court (world 3) is much deadlier than the worlds before it:** its enemies have 60% more health, move and attack faster, bring an extra fighter into every room and field their specialists more often, and its guardians are tougher too. The **minimap** in the top-right corner fills in as you explore.

### Combat tips

- **Enemies:** red ashlings chase you, armoured brutes are slow but tough, and hooded casters keep their distance and flash white just before they fire. From floor 2, small amber **ash skitters** rush you fast but die to a single hit. From floor 3, glowing **cinder husks** are slow and hardy. When one dies, its core flashes a warning ring and then bursts, hurting any other enemies nearby (never you). Kill husks next to their friends. Husks drop 2 crystals.
- **Rear hits:** hitting an enemy from behind shows a pink double chevron. The Assassin deals double damage from behind.
- **Critical hits:** every hero starts with a 5% chance of a physical critical hit, which deals double damage.
- **Elemental effects:** fire sets enemies burning (three ticks, each half the hit's damage), ice freezes them solid, and lightning shocks every other enemy within 2 units for a quarter of the hit. The Wizard's Inferno Orb, chain lightning and Frost Nova always set off their element. Guardians thaw and shrug off freezes faster.

## Heroes

| Hero | Left click / right click | Artifact abilities |
| --- | --- | --- |
| **Knight** | Charged sweeping slash / shield that parries and reflects bolts | Shield Rush, Earthshatter, Aegis, Shield Throw, Whirlwind, War Banner* |
| **Archer** | Charged arrow / triple shot | Arrow Volley, Piercing Shot, Windstep, Net Shot, Ricochet Arrow, Bear Trap* |
| **Wizard** | Charged fireball / chain lightning | Inferno Orb, Frost Nova, Arcane Blink, Ice Wall, Ball Lightning, Lightning Storm* |
| **Assassin** | Charged dagger stab / Shadowstep blink that backstabs everything it passes | Fan of Knives, Venom Vial, Shadow Veil, Smoke Bomb, Death Mark*, Shadow Clone* |
| **Paladin** | Quick sword swipe (hold to bless nearby allies with bonus damage) / Holy Sword strikes | Healing Light, Judgment, Sanctuary, Holy Lance, Consecration, Divine Intervention* |
| **Brawler** | Fast jab (charge for a punch barrage) / Empower | Knuckle Sandwich, Wild Leap, Primal Rage, Thunder Clap, Haymaker Dash, Suplex* |
| **Demoness** | Tail stab (charge to strike the vitals and paralyse) / tail sweep that hits paralysed foes twice as hard | Archdemon's Technique, HEEEELP, Demon Curse, Wing Dash, Soul Siphon, Nightmare Snap* |
| **Gambler** | Throw a coin / a volley of one coin per coin you carry across a 90° cone | Windfall, All In, Jackpot, Card Toss, Dice Bomb, Insurance* |
| **Augment** | Plasma ray that pierces every enemy in a line (charge it for a longer, wider, stronger ray) / hold to charge the arm cannon, release to fire a plasma orb that bursts in flame | Micro-Missiles, Rocket Boost, Sentry Turret, EMP Pulse, Grapple Arm, Orbital Laser* |
| **Specimen** | Changes with his form (see below): palm strike / Flinch Guard while frail, heavy hands and kicks / Arm Guard as the Behemoth, chain whip / Hook as the Edge | Heartbeat, Fight or Flight; Behemoth: Bulldoze, Boulder Toss, Iron Skin, Giant Swing\*; Edge: Swing Line, Ankle Wrap, Bind, Round-Up\* |

\* Bought in the Ash shop before guardians can offer it.

Enemies can now be **immobilized**: paralysed, frozen, stunned (Holy Lance, Thunder Clap, EMP Pulse) or rooted (Net Shot, Bear Trap; rooted enemies can still attack). Talents that care about immobilized enemies count all four.

**Specimen:** an escaped lab subject who starts every descent frail (3 HP) and grows into whatever his talents feed. While he's frail, every floor offers him one **Bulk** talent and one **Edge** talent, and a bar under his health shows which way he leans. The first path to reach three picks claims him for the rest of the descent, and six picks grow him again. Talents left on the other path wither into max HP.
- **Behemoth (Bulk):** 1.35× size, +4 HP and a little slower. Tapping runs Palm Shove, Stomp Kick and Ground Pound; a full charge is an **Axe Kick** that cracks the floor in a line and stuns. Any enemy he knocks into a wall or another enemy is **wall-slammed** (+1 damage and a short stun). Hold right click for his **Arm Guard**: it soaks enemy bolts as **Force** (up to 5) and pushes whatever stands in front of him along, and letting go **Repels**, throwing every stored bolt back in a fan. Enemies near him go for him first. At six picks he becomes the **Colossus** (1.6× size, +3 HP, 8 Force and a 180° guard).
- **Edge:** stays his size, faster, with +1 HP and +10% crit chance. His chain whip's tip is a sweet spot that always crits, and a full charge spins a **Chain Cyclone**. Right click throws a **Hook** that yanks an enemy to his feet, stunned (guardians and brutes are too heavy, so it pulls him to them instead). At six picks every lash cracks twice and the hook drags in two enemies.

**Specimen artifacts:** Heartbeat and Fight or Flight work in any form and change with it. The others only work in their own form and sleep until he takes it.
- **Heartbeat:** a thump of force knocks back everything near him. The Behemoth's reaches farther and stuns; the Edge's leaves enemies taking crits from every hit for 2 seconds.
- **Fight or Flight:** 4 seconds of +40% speed and an instant dodge reset. The Behemoth also gains 2 Force; the Edge also attacks 50% faster.
- **Bulldoze:** charges with his arms crossed, carrying every enemy in his path, and slams them where the charge stops. Bolts that hit his arms on the way become Force.
- **Boulder Toss:** rips up a chunk of floor and hurls it. It shatters on the first enemy and leaves a rock wall that stops bolts for 4 seconds.
- **Iron Skin:** for 4 seconds the next 3 hits do nothing, and each one sends a shockwave through the enemies around him.
- **Giant Swing\*:** swings the nearest enemy round like a club, then throws it. Guardians can't be lifted, so they take a huge punch instead.
- **Swing Line:** the chain bites the first enemy or wall toward the cursor and zips him there, kicking whatever waits at the end.
- **Ankle Wrap:** a low lash across a half circle trips every enemy it reaches.
- **Bind:** the chain wraps the enemy nearest the cursor, rooting it, and every hit on it crits while it's bound.
- **Round-Up\*:** hooks up to four enemies in a wide cone and smashes them together in front of him.

**Demoness artifacts:**
- **Archdemon's Technique:** for 8 seconds every click is a vital stab, and a full charge becomes a paralysing tail whip.
- **HEEEELP:** her giant demonic pet slams its paw down through a portal.
- **Demon Curse:** a pentagram paralyses enemies for 3 seconds, and they take 50% more damage for 6.

**Gambler:** throwing coins doesn't spend them. Every enemy that dies drops a gold coin to pick up, and his magical purse gives him a coin whenever he runs out. Press **R** to open the purse and spend coins on a healing draught, a lucky charm (+1 ward), loaded dice (+1 damage this floor) or Double or Nothing. Every coin spent is one fewer in the volley.

**Gambler artifacts:**
- **Windfall:** instantly gain 5 coins.
- **All In:** double or nothing on every coin you carry.
- **Jackpot:** spend every coin. It always pays out: a slot reel over your head shows whether you won a speed buff, damage buff or heal that grows with each coin spent.

**Augment:** a soldier rebuilt with steel and plasma. His ray is instant and stops only at walls, so line enemies up. Hold right click to charge the cannon: the longer you hold, the bigger and harder the blast, and a full charge always sets enemies burning. Rolling or using a relic cancels the charge without spending the cooldown.

**Augment artifacts:**
- **Micro-Missiles:** a fan of homing missiles that seek out the nearest enemies.
- **Rocket Boost:** dash on your leg thrusters, ramming through enemies, and land in a burst of flame.
- **Sentry Turret:** plant a turret that fires a piercing plasma ray at the nearest enemy for 6 seconds.

## Class mechanics (R)

Once you have beaten the third guardian (floor 15) in a single descent, the Ash shop sells each hero's class mechanic for 600 Ash:

| Hero | Mechanic |
| --- | --- |
| **Knight** | **Shield Taunt:** turn red with rage for 2.25 s (you can walk, but nothing else). It blocks bolts from every side within 2 units and gives a ward for each one, and enemies go for you first. |
| **Archer** | **Elemental Quiver:** cycle between fire, freeze and shock arrows. Arrows still crit, and a critical hit also sets off the arrow's element. |
| **Wizard** | **Wild Storm:** after 8 elemental effects, summon a storm over you that takes turns hurling fireballs, lightning and ice bolts for 10 s. Every storm strike is sure to burn, shock or freeze. |
| **Assassin** | **Sharpened Dagger:** +1 damage for 7.5 s. Every hit on an enemy's back adds +1 more and refreshes it. |
| **Paladin** | **Heavenly Host:** after your blessings add 50 bonus damage (more in co-op), angels revive the ally who has been down longest with half health, or heal the weakest ally to full. |
| **Brawler** | **Super Angry:** after taking 5 damage, go berserk with huge speed, reach, area, charge speed and damage for 8 s. |
| **Demoness** | **Demonic Power:** after 7 paralyses, the Demon Lord's massive head rises behind you and roars, terrifying every enemy around you: they turn their backs and are paralysed. |
| **Augment** | **Overclock:** after your plasma ray strikes 25 enemies, overclock for 8 s: every ray is fully charged and fires twice as fast, and the cannon is ready at once, fires fully charged and cools down twice as fast. |
| **Specimen** | **Breaking Point:** fills as he takes hits and soaks bolts (frail or Behemoth) or lands critical hits (Edge). The Behemoth **Rampages** for 8 s (2.2× size, every blow a Ground Pound, walking through enemies throws them aside, every 4 wall slams heal 1 HP); the Edge goes into **Overdrive** for 6 s (twice the attack speed, a burst from every lash's tip, half the dodge cooldown); while frail he **Snaps** into the form he leans toward for 10 s. |

## Ash and the Ash shop

You earn **Ash** from every kill (1 Ash), every cleared floor (10 Ash) and every guardian (50 Ash). Spend it in the **Ash shop** on the main menu on permanent upgrades such as health, damage, speed, attack speed and dodge. There are also class-specific upgrades for every hero, and some of each hero's abilities are unlocked here. New purchases apply from your next run.

The shop lists upgrades two to a row, with the ones you can buy first, locked ones after and maxed ones last (**Hide maxed** tucks those away). A gold dot on a hero's tab means something there is affordable right now.

Clearing a world for the first time (with any hero) adds two rewards to the shop:

| World | Rewards |
| --- | --- |
| The Ash Below | **Ember Heart:** start every descent with a random talent |
| The Neon Arcology | **Backup Drive:** once per descent a killing blow leaves you at 1 HP. **Targeting Chip:** +3% crit and effect chance per rank |
| The Infernal Court | **Infernal Pact:** +10% damage and -10% max HP per rank (up to 90%), with an on/off switch. **Soul Tithe:** guardians drop a heart that heals 2 HP |
| The Arcane Spire | **Scholar's Reroll:** reroll the floor talent pick once per world. **Sanctified Relics:** guardians offer 4 abilities |
| The Shadow Market | **Black Market Pass:** crystal shops stock an extra relic. **Smuggler's Stash:** keep 25% of unspent crystals (up to 100) for the next descent |
| The Savage Wilds | **Wild Growth:** +1 max HP for each world cleared in a descent. **Apex Predator:** +10% damage to guardians and brutes per rank |

## Co-op (up to 4 players)

Choose **Co-op** in the main menu. There are two ways to connect:

- **Host a party (online):** the host gets a short **join code**, and friends enter it under **Join code**. No port forwarding is needed.
- **Host P2P / Join IP (direct):** the host shares the address shown in the lobby (use the **Copy** button), and friends paste it and press **Join IP**. The game tries to open UDP port **7777** on the host's router automatically. If the lobby says that failed, friends on the same network can still join. Anyone else needs port 7777 forwarded by hand, or should use **Host a party** instead.

On Windows, allow **AshBelow** through the firewall the first time you host.

Everyone picks a hero in the lobby, then the host presses **Begin descent**.

- Enemies get tougher for each extra player, and the game doesn't pause.
- A fallen hero comes back with half health on the next floor. The run ends only when the whole party is down.
- Each player collects and spends their own crystals. The crystal shop's stairs only work once the whole party is standing at them.
- Each player earns Ash into their own save.
- The host's **Restart** button restarts for everyone. A guest's Restart counts as a vote.
- You can't join a party once its descent has started.

## Updating

Close the game and run **`Update.cmd`** from the game folder. It downloads the latest release and replaces your current install with it, so you launch the game the same way as before. If the update fails partway, the old version is put back. Your save is backed up first and is never reset.

`Update.cmd` needs Python. If Python isn't installed, it downloads a portable copy into `%LOCALAPPDATA%\AshBelow\python` without needing admin rights. Delete that folder to remove it.

On Linux, run **`sh Update.sh`** instead. It works the same way and installs the latest Linux build. It needs Python 3.10 or newer, which most distributions already include. If yours doesn't, install `python3` with your package manager.

## Your save

Your progress is saved automatically after every reward and purchase, at:

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\Ash Below\progress.json
```

On Linux it's at `~/.config/unity3d/DefaultCompany/Ash Below/progress.json`.

A backup (`progress.json.bak`) is kept next to it, and the game recovers from it automatically if the main save is damaged. Deleting or moving the game folder does not touch your save.

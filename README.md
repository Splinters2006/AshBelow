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
- **Pick a boon.** Between floors you choose one of three upgrades for this run. Each choice also restores 2 HP. Some boons belong to your class, and some only show up after you unlock the ability they improve. Open **Talents** in the HUD to see your build.
- **Gather crystals.** Every enemy you defeat drops purple crystals (armoured brutes drop 3, guardians 15). Walk near them to pick them up. They last the whole run.
- **Visit the crystal shop.** Before every guardian you reach a lantern-lit shop. The merchant glows when you can talk to him. Press **F** at his counter to spend crystals. Each shop stocks a random five wares:
  - **One healing ware:** a Healing Draught (+2 HP) or a Grand Elixir (full HP).
  - **Two boss-fight boons,** which last only for the next guardian: Stoneskin Tonic (+2 wards), Whetstone (+1 damage) or Quicksilver (+20% move speed).
  - **Two relics,** which last the rest of the run: Heart Crystal (+1 max HP), Ember Hone (+1 damage), Windrunner Boots (+0.7 speed), Quickfinger Gloves (+20% attack speed), Hawkeye Lens (+10% crit chance), Warding Sigil (+1 ward every floor), Vampire Fang (a rank of Soul Harvest) or Phoenix Feather (shorter dodge cooldown). Relics add ranks to the matching boon and stop selling once it's maxed.

  Each ware costs 50% more every time you buy it in the same shop. In co-op, everyone sees the same stock. The shop's stairs lead to the guardian.
- **Beat the guardians.** Every fifth floor is a boss arena. Three guardians take turns, and each gets more dangerous below half health:
  - **The Rime Warden**, a frost caster who fills the arena with icicle fans, frost novas, blizzard spirals, hailstorms and closing rings of ice.
  - **The Steel Duelist**, a fast, fragile swordsman who dashes, throws blade fans, teleports behind you and cuts crosses of blade light.
  - **Malphas, the Hellfire Archdemon**. Watch for **Cataclysm**: the floor turns red, then everything except one golden circle erupts in fire. A gold arrow at your feet points to the circle.
- **Claim an artifact.** A beaten guardian drops an artifact that unlocks an active ability for your class. Put it on **Q** or **E**. You can hold two at a time, and later artifacts can upgrade one (up to rank 3) or replace it. You can also leave an artifact for 40 crystals.
- **Death ends the run.** Boons and abilities reset, but your **Ash** and Ash shop upgrades are kept.

Enemies get tougher every floor. The **minimap** in the top-right corner fills in as you explore.

### Combat tips

- **Enemies:** red ashlings chase you, armoured brutes are slow but tough, and hooded casters keep their distance and flash white just before they fire. From floor 2, small amber **ash skitters** rush you fast but die to a single hit. From floor 3, glowing **cinder husks** are slow and hardy. When one dies, its core flashes a warning ring and then bursts, hurting any other enemies nearby (never you). Kill husks next to their friends. Husks drop 2 crystals.
- **Rear hits:** hitting an enemy from behind shows a pink double chevron. The Assassin deals double damage from behind.
- **Critical hits:** every hero starts with a 5% chance of a physical critical hit, which deals double damage.
- **Elemental effects:** fire sets enemies burning (three ticks, each half the hit's damage), ice freezes them solid, and lightning shocks every other enemy within 2 units for a quarter of the hit. The Wizard's Inferno Orb, chain lightning and Frost Nova always set off their element. Guardians thaw and shrug off freezes faster.

## Heroes

| Hero | Left click / right click | Artifact abilities |
| --- | --- | --- |
| **Knight** | Charged sweeping slash / shield that parries and reflects bolts | Shield Rush, Earthshatter, Aegis |
| **Archer** | Charged arrow / triple shot | Arrow Volley, Piercing Shot, Windstep |
| **Wizard** | Charged fireball / chain lightning | Inferno Orb, Frost Nova, Arcane Blink |
| **Assassin** | Charged dagger stab / Shadowstep blink that backstabs everything it passes | Fan of Knives, Venom Vial, Shadow Veil |
| **Paladin** | Quick sword swipe (hold to bless nearby allies with bonus damage) / Holy Sword strikes | Healing Light, Judgment, Sanctuary |
| **Brawler** | Fast jab (charge for a punch barrage) / Empower | Knuckle Sandwich, Wild Leap, Primal Rage |
| **Demoness** | Tail stab (charge to strike the vitals and paralyse) / tail sweep that hits paralysed foes twice as hard | Archdemon's Technique, HEEEELP, Demon Curse |
| **Gambler** | Throw a coin / a volley of one coin per coin you carry across a 90° cone | Windfall, All In, Jackpot |
| **Admin** | Shadow rifts / Nightfall, which executes every nearby enemy | Eclipse, Soul Rend, Shadow Reign |

**Admin** is deliberately overpowered, for when you just want to wreck things.

**Demoness artifacts:**
- **Archdemon's Technique:** for 8 seconds every click is a vital stab, and a full charge becomes a paralysing tail whip.
- **HEEEELP:** her giant demonic pet slams its paw down through a portal.
- **Demon Curse:** a pentagram paralyses enemies for 3 seconds, and they take 50% more damage for 6.

**Gambler:** throwing coins doesn't spend them. Every enemy that dies drops a gold coin to pick up, and his magical purse gives him a coin whenever he runs out. Press **R** to open the purse and spend coins on a healing draught, a lucky charm (+1 ward), loaded dice (+1 damage this floor) or Double or Nothing. Every coin spent is one fewer in the volley.

**Gambler artifacts:**
- **Windfall:** instantly gain 5 coins.
- **All In:** double or nothing on every coin you carry.
- **Jackpot:** spend every coin. It always pays out: a slot reel over your head shows whether you won a speed buff, damage buff or heal that grows with each coin spent.

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

## Ash and the Ash shop

You earn **Ash** from every kill (1 Ash), every cleared floor (10 Ash) and every guardian (50 Ash). Spend it in the **Ash shop** on the main menu on permanent upgrades such as health, damage, speed, attack speed and dodge. There are also class-specific upgrades for every hero. New purchases apply from your next run.

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

Close the game and run **`Update.cmd`** from the game folder. It downloads the latest release and installs it next to your current one, so you can go back if needed. It then prints where the new version is. Your save is backed up first and is never reset.

`Update.cmd` needs Python. If Python isn't installed, it downloads a portable copy into `%LOCALAPPDATA%\AshBelow\python` without needing admin rights. Delete that folder to remove it.

On Linux, run **`sh Update.sh`** instead. It works the same way and installs the latest Linux build. It needs Python 3.10 or newer, which most distributions already include. If yours doesn't, install `python3` with your package manager.

## Your save

Your progress is saved automatically after every reward and purchase, at:

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\Ash Below\progress.json
```

On Linux it's at `~/.config/unity3d/DefaultCompany/Ash Below/progress.json`.

A backup (`progress.json.bak`) is kept next to it, and the game recovers from it automatically if the main save is damaged. Deleting or moving the game folder does not touch your save.

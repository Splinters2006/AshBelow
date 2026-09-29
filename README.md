# Ash / Below

An action roguelike. Pick a hero, fight through connected dungeon rooms, beat a guardian every five floors, and see how deep you can go. Play alone or with up to three friends.

## Install and play

1. Download the latest Windows release ZIP.
2. Extract it anywhere and run **`AshBelow.exe`**. Keep the other files and folders from the ZIP next to it.
3. In the main menu, pick **Choose your hero → a class → Begin descent**.

The game starts in a resizable 1280×720 window.

## Controls

| Action | Default |
| --- | --- |
| Move | **WASD** or **arrow keys** |
| Aim | Mouse cursor |
| Attack | **Left click**. Tap for a quick attack, hold to charge and release for a stronger one. |
| Class skill | **Right click** |
| Dodge roll | **Space** (you can't be hit during the roll) |
| Relic abilities | **Q** and **E** |
| Use stairs / claim artifact | **F** |

You can rebind every action under **Controls** in the main menu. Each action gets a main and an alternate key or mouse button. The HUD always shows your current keys.

## How a run works

- **Clear the floor.** Defeat every enemy, then find the gold stairs and press **F** to descend.
- **Pick a boon.** Between floors you choose one of three upgrades for this run. Each choice also restores 2 HP. Some boons belong to your class, and some only show up after you unlock the ability they improve. Open **Talents** in the HUD to see your build.
- **Beat the guardians.** Every fifth floor is a boss arena. Three guardians take turns, and each gets more dangerous below half health:
  - **The Ash Warden**, a caster who fills the arena with ember fans, novas, spirals and falling fire.
  - **The Ashen Duelist**, a fast, fragile swordsman who dashes, throws blade fans and teleports behind you.
  - **Malphas, the Hellfire Archdemon**. Watch for **Cataclysm**: the floor turns red, then everything except one golden circle erupts in fire. A gold arrow at your feet points to the circle.
- **Claim an artifact.** A beaten guardian drops an artifact that unlocks an active ability for your class. Put it on **Q** or **E**. You can hold two at a time, and later artifacts can upgrade one (up to rank 3) or replace it.
- **Death ends the run.** Boons and abilities reset, but your **Ash** and Ash shop upgrades are kept.

Enemies get tougher every floor. The **minimap** in the top-right corner fills in as you explore.

### Combat tips

- **Enemies:** red ashlings chase you, armoured brutes are slow but tough, and hooded casters keep their distance and flash white just before they fire.
- **Rear hits:** hitting an enemy from behind shows a pink double chevron. The Assassin deals double damage from behind.
- **Critical hits:** every hero starts with a 5% chance of a physical critical hit, which deals double damage.
- **Elemental effects:** fire can set enemies burning, ice slows them, and lightning can chain between enemies.

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
| **Admin** | Shadow rifts / Nightfall, which executes every nearby enemy | Eclipse, Soul Rend, Shadow Reign |

**Admin** is deliberately overpowered, for when you just want to wreck things.

**Demoness artifacts:**
- **Archdemon's Technique:** for 8 seconds every click is a vital stab, and a full charge becomes a paralysing tail whip.
- **HEEEELP:** her giant demonic pet slams its paw down through a portal.
- **Demon Curse:** a pentagram paralyses enemies for 3 seconds, and they take 50% more damage for 6.

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
- Each player earns Ash into their own save.
- The host's **Restart** button restarts for everyone. A guest's Restart counts as a vote.
- You can't join a party once its descent has started.

## Updating

Close the game and run **`Update.cmd`** from the game folder. It downloads the latest release and installs it next to your current one, so you can go back if needed. It then prints where the new version is. Your save is backed up first and is never reset.

`Update.cmd` needs Python. If Python isn't installed, it downloads a portable copy into `%LOCALAPPDATA%\AshBelow\python` without needing admin rights. Delete that folder to remove it.

## Your save

Your progress is saved automatically after every reward and purchase, at:

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\Ash Below\progress.json
```

A backup (`progress.json.bak`) is kept next to it, and the game recovers from it automatically if the main save is damaged. Deleting or moving the game folder does not touch your save.

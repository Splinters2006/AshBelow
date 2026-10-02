namespace Slopgame
{
    /// <summary>
    /// What the hero select screen calls each hero's charged attack and right click, and how it describes them.
    /// (Guardian artifacts describe themselves in <see cref="AbilityCatalog"/>, and class mechanics in the Ash shop.)
    /// </summary>
    public static class HeroMoves
    {
        public static string ChargedName(WeaponType weapon) => weapon switch
        {
            WeaponType.Sword => "Full slash",
            WeaponType.Bow => "Drawn arrow",
            WeaponType.Staff => "Charged fireball",
            WeaponType.Daggers => "Piercing thrust",
            WeaponType.Hammer => "Blessing",
            WeaponType.Fists => "Barrage",
            WeaponType.Tail => "Vital stab",
            WeaponType.Coins => "Weighted coin",
            WeaponType.Beam => "Charged ray",
            WeaponType.Scythe => "Harvest",
            WeaponType.Katana => "Flurry",
            _ => "Charged attack"
        };

        public static string ChargedDescription(WeaponType weapon) => weapon switch
        {
            WeaponType.Sword => "Hold the attack to charge: the sword's sweep widens to twice its usual cone and hits harder.",
            WeaponType.Bow => "Hold the attack to draw the bow: the arrow flies farther and hits harder.",
            WeaponType.Staff => "Hold the attack to charge your fireball: it swells in your hand and hits far harder.",
            WeaponType.Daggers => "Hold the attack to charge: the thrust narrows to a needle and hits far harder.",
            WeaponType.Hammer => "A full charge is a blessing, not a blow: you and every ally near you deal extra damage for a few seconds.",
            WeaponType.Fists => "A slow full charge releases a barrage of punches across a bigger rectangle. The last one sends enemies flying.",
            WeaponType.Tail => "A full charge strikes the vitals: the enemy is paralysed for 1.5 seconds.",
            WeaponType.Coins => "Hold the attack to weigh the coin in your hand: it hits harder.",
            WeaponType.Beam => "Hold the attack to charge the plasma ray: it reaches farther and grows nearly three times as wide.",
            WeaponType.Scythe => "A full charge is a harvest: the scythe sweeps wider and pulls a soul out of every enemy it cuts.",
            WeaponType.Katana => "A full charge looses a flurry of six rapid slashes, each worth half a cut.",
            _ => "Hold the attack to charge it."
        };

        public static string HeavyDescription(WeaponType weapon) => weapon switch
        {
            WeaponType.Sword => "Raise your shield for a quarter of a second: any bolt it catches is thrown straight back at whoever fired it.",
            WeaponType.Bow => "Three arrows in a tight, long-range spread.",
            WeaponType.Staff => "A bolt of lightning snaps to the nearest enemy you are facing.",
            WeaponType.Daggers => "Step through the shadows toward the cursor, cutting every enemy you pass through. Strike from behind for a backstab.",
            WeaponType.Hammer => "Holy swords fall from the sky onto every enemy near you, one after another, slowing whatever they strike.",
            WeaponType.Fists => "Empower yourself: for 5 seconds you hit harder, faster and wider.",
            WeaponType.Tail => "Sweep your tail across a half circle. Immobilized enemies take triple damage.",
            WeaponType.Coins => "Throw one coin for every coin you carry across a 90-degree cone, without spending any.",
            WeaponType.Beam => "Hold to charge the arm cannon and release to fire an orb that bursts in flame.",
            WeaponType.Scythe => "Spend souls on homing skulls: tap for one, hold for a fearsome one, and hold to the full for three.",
            WeaponType.Katana => "Dash toward the cursor and slice everything in your path for double damage.",
            _ => "Guard as the Behemoth, or hook as the Edge."
        };
    }
}

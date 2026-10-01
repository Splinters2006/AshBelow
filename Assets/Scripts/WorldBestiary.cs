using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Each world's specialist enemies. The first entry is the world's signature specialist, guaranteed in the first
    /// combat room from floor 3; the rest turn up at random. Picks come from the floor's seeded stream, so every co-op
    /// machine spawns the same ones.
    /// </summary>
    public static class WorldBestiary
    {
        public static int RosterSize(WorldDefinition world) => Roster(world.Index).Length;

        public static System.Type[] Roster(int worldIndex) => worldIndex switch
        {
            0 => new[] { typeof(EmberFanatic), typeof(SootLobber), typeof(MagmaStomper), typeof(CinderDasher) },
            1 => new[] { typeof(NeonLancer), typeof(SniperDrone), typeof(MineCrawler) },
            2 => new[] { typeof(BrimstoneCultist), typeof(BlinkImp) },
            3 => new[] { typeof(BlinkMagus), typeof(OrbitingEye) },
            4 => new[] { typeof(Cutthroat), typeof(BladeJuggler), typeof(Shadowstepper) },
            5 => new[] { typeof(BloatToad), typeof(WildRaptor) },
            _ => new[] { typeof(EmberFanatic) }
        };

        /// <summary>Turns a freshly spawned basic enemy into one of its world's specialists.</summary>
        public static EnemyVariant AddSpecialist(GameObject enemy, WorldDefinition world, bool signature, System.Random random)
        {
            var roster = Roster(world.Index);
            var type = signature ? roster[0] : roster[random.Next(roster.Length)];
            return (EnemyVariant)enemy.AddComponent(type);
        }
    }
}

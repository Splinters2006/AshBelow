using UnityEngine;

namespace Slopgame
{
    /// <summary>An orbital bullet pattern fight: rotating spokes, paired satellites and offset pulse waves.</summary>
    public sealed class SingularityCoreBoss : NeonBossBehaviour
    {
        public override string Title => "NULL, THE SINGULARITY CORE";
        protected override Color Accent => new Color(0.75f, 0.35f, 1f);
        protected override Sprite Chassis => NeonBossSprites.Core;
        protected override HazardStyle Hazards => HazardStyle.Void;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells = { "SPIRAL STREAM - WEAVE THROUGH THE SPOKES", "SATELLITE CROSSFIRE - WATCH BOTH SIDES", "PULSE ECHO - DODGE EACH WAVE" };
        private float nextShot, angle;
        private int volley;
        public override int BaseHealth(int floor) => 26 + floor * 2;
        protected override float Attack(int index, Vector2 aim)
        {
            angle = Mathf.Atan2(aim.y, aim.x); volley = 0;
            nextShot = Enemy.ActionTime + 1f;
            if (index == 2)
                for (int i = 0; i < (IsEnraged ? 3 : 2); i++)
                    Hazard(HazardShape.Ring, transform.position, aim, 17f, 0.65f, 1f + i * 0.85f, 2.8f);
            return index == 2 ? 5.6f : 4f;
        }
        protected override void AttackTick()
        {
            if (state == 3 || Enemy.ActionTime < nextShot || volley >= (IsEnraged ? 9 : 7)) return;
            nextShot = Enemy.ActionTime + 0.32f;
            Vector2 center = transform.position;
            if (state == 1)
            {
                for (int i = 0; i < 4; i++) Fire(center, FlameMesh.Polar(angle + i * Mathf.PI * 0.5f, 1f), 5f);
                angle += 0.22f;
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 origin = center + Vector2.right * side * 2.4f;
                    Fire(origin, (Run.NearestHero(origin) - origin).normalized, IsEnraged ? 7f : 5.5f);
                }
            }
            volley++;
        }
        protected override void DrawCircuitry(FlameMesh mesh, Vector2 center, float intensity)
        {
            mesh.Ring(center, 1.1f, 0.09f, FlameMesh.Alpha(Accent, intensity), 48);
            for (int i = 0; i < 3; i++)
            {
                Vector2 orbit = center + FlameMesh.Polar(Time.time * 1.8f + i * Mathf.PI * 2f / 3f, 1.4f);
                mesh.Diamond(orbit, 0.22f, Accent);
            }
            if (state == 2)
                for (int side = -1; side <= 1; side += 2)
                    mesh.Ring(center + Vector2.right * side * 2.4f, 0.4f, 0.12f, Accent, 20);
        }
    }
}

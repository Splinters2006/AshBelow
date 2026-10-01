using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Specimen's class mechanic (R): a meter that fills as he takes hits and soaks bolts (frail or Behemoth) or lands
    /// critical hits (Edge). When it is full he breaks loose, and how depends on his form:
    /// the Behemoth Rampages (double size, every blow a Ground Pound, trampling what he walks through, healing from
    /// wall slams), the Edge goes into Overdrive (twice the attack speed, a burst from every lash's tip, half the dodge
    /// cooldown), and while frail he Snaps into the form he leans toward for a while.
    /// Its R upgrade, Apex Mutation, ends a Rampage in a roar that draws every enemy and stuns everything nearby, and
    /// stretches an Overdrive with every kill.
    /// </summary>
    public sealed class BreakingPoint : ChargedMechanic
    {
        public const int Charges = 10;
        public override string Name => "Breaking Point";
        public override Color Color => SpecimenCatalog.Amber;
        public override int Required => Charges;
        private SpecimenAttack Specimen => Player != null ? Player.Weapon as SpecimenAttack : null;

        public override string Status
        {
            get
            {
                var specimen = Specimen;
                if (specimen != null && specimen.IsRampaging) return $"RAMPAGE {specimen.RampageRemaining:0.0}s";
                if (specimen != null && specimen.IsOverdriven) return $"OVERDRIVE {specimen.OverdriveRemaining:0.0}s";
                if (specimen != null && specimen.IsSnapped) return $"SNAPPED {specimen.SnapRemaining:0.0}s";
                return base.Status;
            }
        }

        /// <summary>Soaked bolts and critical hits charge the meter (see <see cref="SpecimenAttack.FeedMechanic"/>).</summary>
        public void Feed(int amount) => AddCharge(amount);

        /// <summary>Hits he takes charge it while frail or as the Behemoth; the Edge charges it with crits instead.</summary>
        public override void OnDamaged()
        {
            var specimen = Specimen;
            if (specimen != null && specimen.Form != SpecimenForm.Edge && !specimen.IsBreaking) AddCharge(1);
        }

        protected override bool Activate(Vector2 aim)
        {
            var specimen = Specimen;
            if (specimen == null || specimen.IsBreaking) return false;
            switch (specimen.Form)
            {
                case SpecimenForm.Behemoth: specimen.BeginRampage(SpecimenAttack.RampageTime); break;
                case SpecimenForm.Edge: specimen.BeginOverdrive(SpecimenAttack.OverdriveTime); break;
                default: specimen.BeginSnap(SpecimenAttack.SnapTime); break;
            }
            return true;
        }
    }
}

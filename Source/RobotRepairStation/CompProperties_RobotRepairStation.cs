using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RobotRepairStation
{
    public class CompProperties_RobotRepairStation : CompProperties
    {
        public float repairHealthThreshold = 0.5f;

        public float repairSpeedPerTick = 0.0005f;

        public int steelPerRepairCycle = 1;

        public int repairTickInterval = 500;

        public float maxRepairRange = 30f;

        public CompProperties_RobotRepairStation()
        {
            compClass = typeof(CompRobotRepairStation);
        }
    }

    public class CompRobotRepairStation : ThingComp
    {
        public CompProperties_RobotRepairStation Props =>
            (CompProperties_RobotRepairStation)props;

        private Building_RobotRepairStation Station =>
            (Building_RobotRepairStation)parent;

        public override void CompTick()
        {
            base.CompTick();

            if (!Station.HasPower) return;
            if (!Station.IsOccupied) return;

            var pawn = Station.CurrentOccupant;
            if (pawn == null || pawn.Dead) return;

            if (Find.TickManager.TicksGame % Props.repairTickInterval == 0)
            {
                ApplyRepairTick(pawn);
            }
        }

        private void ApplyRepairTick(Pawn mechanoid)
        {
            List<Hediff_Injury> injuries = mechanoid.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(h => !h.IsOld())
                .ToList();

            foreach (Hediff_Injury injury in injuries)
            {
                injury.Heal(Props.repairSpeedPerTick);
            }

            if (mechanoid.health.summaryHealth.SummaryHealthPercent >= 0.99f)
            {
                OnRepairComplete(mechanoid);
            }
        }

        private void OnRepairComplete(Pawn mechanoid)
        {
            Messages.Message(
                "RRS_LetterRepairCompleteText".Translate(mechanoid.LabelShort),
                mechanoid,
                MessageTypeDefOf.PositiveEvent
            );

            Station.EjectOccupant();
        }
    }
}

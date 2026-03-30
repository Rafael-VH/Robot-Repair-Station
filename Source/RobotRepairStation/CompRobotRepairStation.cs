using RimWorld;
using Verse;

namespace RobotRepairStation
{
    /// <summary>
    /// XML-exposed properties for the repair station comp.
    /// All values are configurable from the ThingDef XML.
    /// </summary>
    public class CompProperties_RobotRepairStation : CompProperties
    {
        /// <summary>
        /// Health fraction (0–1) below which a mechanoid will seek repair.
        /// Default: 0.5 (50% health)
        /// </summary>
        public float repairHealthThreshold = 0.5f;

        /// <summary>
        /// How much health is restored per tick while the mechanoid is docked.
        /// Default: 0.0005 (~20% health per in-game hour at normal speed)
        /// </summary>
        public float repairSpeedPerTick = 0.0005f;

        /// <summary>
        /// Amount of steel consumed per repair cycle interval.
        /// </summary>
        public int steelPerRepairCycle = 1;

        /// <summary>
        /// How many ticks between each repair cycle (steel consumption + health tick).
        /// </summary>
        public int repairTickInterval = 500;

        /// <summary>
        /// Maximum distance (in cells) at which a mechanoid will detect this station.
        /// </summary>
        public float maxRepairRange = 30f;

        public CompProperties_RobotRepairStation()
        {
            compClass = typeof(CompRobotRepairStation);
        }
    }

    /// <summary>
    /// Comp attached to the repair station building.
    /// Handles per-tick repair logic applied to the current occupant.
    /// </summary>
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

            // Apply repair each tick
            ApplyRepairTick(pawn);
        }

        private void ApplyRepairTick(Pawn mechanoid)
        {
            // Heal all injuries slightly each tick
            foreach (var hediff in mechanoid.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury injury && !injury.IsOld())
                {
                    injury.Heal(Props.repairSpeedPerTick);
                }
            }

            // Check if fully healed
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

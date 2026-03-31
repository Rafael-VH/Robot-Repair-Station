using System.Collections.Generic;
using System.Linq;
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
        /// How much health is restored per repair interval while the mechanoid is docked.
        /// Default: 0.0005 per tick (~20% health per in-game hour at normal speed)
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
    ///
    /// FIX #1: ApplyRepairTick now runs only every repairTickInterval ticks,
    ///         not every single tick.
    /// FIX #2: OnRepairComplete is the single authoritative eject point.
    ///         JobDriver_RepairAtStation no longer calls EjectOccupant itself.
    /// FIX #7: hediff collection is copied to a List before iteration to avoid
    ///         InvalidOperationException when Heal() removes a fully-healed hediff.
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

            // FIX #1: Only heal on the configured interval, not every tick.
            if (Find.TickManager.TicksGame % Props.repairTickInterval == 0)
            {
                ApplyRepairTick(pawn);
            }
        }

        private void ApplyRepairTick(Pawn mechanoid)
        {
            // FIX #7: Copy hediffs to a separate list before iterating.
            // Heal() can remove a fully-healed injury from the live collection,
            // which would throw InvalidOperationException mid-loop.
            List<Hediff_Injury> injuries = mechanoid.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(h => !h.IsOld())
                .ToList();

            foreach (Hediff_Injury injury in injuries)
            {
                injury.Heal(Props.repairSpeedPerTick);
            }

            // FIX #2: Check completion here only; the job driver detects the
            // eject indirectly by noticing CurrentOccupant != pawn.
            if (mechanoid.health.summaryHealth.SummaryHealthPercent >= 0.99f)
            {
                OnRepairComplete(mechanoid);
            }
        }

        /// <summary>
        /// Single authoritative point for declaring repair finished.
        /// Sends a positive message and ejects the occupant.
        /// The job driver detects the eject via Station.CurrentOccupant != pawn.
        /// </summary>
        private void OnRepairComplete(Pawn mechanoid)
        {
            Messages.Message(
                "RRS_LetterRepairCompleteText".Translate(mechanoid.LabelShort),
                mechanoid,
                MessageTypeDefOf.PositiveEvent
            );

            // EjectOccupant sets currentOccupant = null and ends the repair job.
            // The job driver's tickAction will then see CurrentOccupant != pawn
            // and call EndJobWith(Succeeded) cleanly.
            Station.EjectOccupant();
        }
    }
}

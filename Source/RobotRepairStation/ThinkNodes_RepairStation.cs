using RimWorld;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    /// <summary>
    /// Think node that fires when a mechanoid needs repair.
    /// Added via XML Patch to the Mechanoid think tree.
    /// </summary>
    public class ThinkNode_ConditionalNeedsRepair : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            // Only applies to player-controlled mechanoids.
            if (!pawn.RaceProps.IsMechanoid) return false;
            if (pawn.Faction != Faction.OfPlayer) return false;

            var station = RepairStationUtility.FindBestRepairStation(pawn);
            if (station == null) return false;

            // FIX #9: GetComp is called once inside FindBestRepairStation;
            // we retrieve the cached result via RepairStationUtility here.
            var comp = station.GetComp<CompRobotRepairStation>();
            float threshold = comp?.Props.repairHealthThreshold ?? 0.5f;

            return pawn.health.summaryHealth.SummaryHealthPercent < threshold;
        }
    }

    /// <summary>
    /// Think node job giver: assigns the GoToRepairStation job.
    ///
    /// FIX #6: Before issuing the job, checks that no other pawn has already
    ///         reserved the target station. This prevents two mechanoids from
    ///         racing to the same station and one failing in a tight loop.
    /// </summary>
    public class JobGiver_GoToRepairStation : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var station = RepairStationUtility.FindBestRepairStation(pawn);
            if (station == null) return null;

            // FIX #6: Skip station if it is already reserved by a different pawn.
            // ReservedBy(station, pawn) returns true if pawn itself holds the
            // reservation (i.e. it already has the GoTo job and is on its way),
            // in which case we allow re-assigning normally.
            var reservationManager = pawn.Map?.reservationManager;
            if (reservationManager != null
                && reservationManager.IsReservedByAnyoneOf(station, pawn.Faction)
                && !reservationManager.ReservedBy(station, pawn))
            {
                return null;
            }

            return JobMaker.MakeJob(RRS_JobDefOf.RRS_GoToRepairStation, station);
        }
    }

    /// <summary>
    /// Utility helpers for finding and evaluating repair stations.
    ///
    /// FIX #9: GetComp&lt;CompRobotRepairStation&gt; is called exactly once per station
    ///         per search, and the result is reused for both Props lookups inside
    ///         the loop — previously the code called it twice per station.
    /// </summary>
    public static class RepairStationUtility
    {
        public static Building_RobotRepairStation FindBestRepairStation(Pawn pawn)
        {
            if (pawn?.Map == null) return null;

            var tracker = RepairStationTracker.GetOrCreate(pawn.Map);
            Building_RobotRepairStation best = null;
            float bestDist = float.MaxValue;

            foreach (var station in tracker.AllStations)
            {
                if (station == null || station.Destroyed) continue;
                if (!station.HasPower) continue;
                if (station.IsOccupied && station.CurrentOccupant != pawn) continue;
                if (!pawn.CanReach(station, PathEndMode.InteractionCell, Danger.Deadly)) continue;

                // FIX #9: Single GetComp call per station; reuse for both lookups.
                var comp = station.GetComp<CompRobotRepairStation>();
                float maxRange = comp?.Props.maxRepairRange ?? 30f;

                float dist = pawn.Position.DistanceTo(station.Position);
                if (dist > maxRange) continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = station;
                }
            }

            return best;
        }
    }
}

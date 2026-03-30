using RimWorld;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    /// <summary>
    /// Think node that fires when a mechanoid needs repair.
    /// Added via Patch to the Mechanoid think tree.
    /// </summary>
    public class ThinkNode_ConditionalNeedsRepair : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            // Only applies to mechanoids under a mechlink (player controlled)
            if (!pawn.RaceProps.IsMechanoid) return false;
            if (pawn.Faction != Faction.OfPlayer) return false;

            var station = RepairStationUtility.FindBestRepairStation(pawn);
            if (station == null) return false;

            var comp = station.GetComp<CompRobotRepairStation>();
            float threshold = comp?.Props.repairHealthThreshold ?? 0.5f;

            return pawn.health.summaryHealth.SummaryHealthPercent < threshold;
        }
    }

    /// <summary>
    /// Think node job giver: assigns the GoToRepairStation job.
    /// </summary>
    public class JobGiver_GoToRepairStation : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var station = RepairStationUtility.FindBestRepairStation(pawn);
            if (station == null) return null;

            return JobMaker.MakeJob(RRS_JobDefOf.RRS_GoToRepairStation, station);
        }
    }

    /// <summary>
    /// Utility helpers for finding and evaluating repair stations.
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

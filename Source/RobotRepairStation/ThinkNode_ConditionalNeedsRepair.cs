using RimWorld;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    public class ThinkNode_ConditionalNeedsRepair : ThinkNode_Conditional
    {
        [Unsaved]
        private Pawn lastEvaluatedPawn;
        [Unsaved]
        private Building_RobotRepairStation cachedStation;

        protected override bool Satisfied(Pawn pawn)
        {
            if (!pawn.RaceProps.IsMechanoid) return false;
            if (pawn.Faction != Faction.OfPlayer) return false;

            var station = RepairStationUtility.FindBestRepairStation(pawn);
            lastEvaluatedPawn = pawn;
            cachedStation = station;

            if (station == null) return false;

            var comp = station.GetComp<CompRobotRepairStation>();
            float threshold = comp?.Props.repairHealthThreshold ?? 0.5f;

            return pawn.health.summaryHealth.SummaryHealthPercent < threshold;
        }

        public Building_RobotRepairStation GetCachedStation(Pawn pawn) => (lastEvaluatedPawn == pawn
        && cachedStation != null
        && !cachedStation.Destroyed)
        ? cachedStation
        : null;
    }

    public class JobGiver_GoToRepairStation : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var conditional = parent as ThinkNode_ConditionalNeedsRepair;
            var station = conditional?.GetCachedStation(pawn)
                       ?? RepairStationUtility.FindBestRepairStation(pawn);

            if (station == null) return null;

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

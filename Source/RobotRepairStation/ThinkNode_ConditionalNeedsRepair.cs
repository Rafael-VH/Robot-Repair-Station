using RimWorld;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    public class ThinkNode_ConditionalNeedsRepair : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            if (!pawn.RaceProps.IsMechanoid)       return false;
            if (pawn.Faction != Faction.OfPlayer)  return false;

            var comp = RepairStationUtility.FindBestRepairStationComp(pawn);
            if (comp == null) return false;

            return pawn.health.summaryHealth.SummaryHealthPercent < comp.Props.repairHealthThreshold;
        }
    }

    public class JobGiver_GoToRepairStation : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Building_RobotRepairStation station =
                RepairStationUtility.FindBestRepairStation(pawn);

            if (station == null) return null;

            // Verificar que nadie más ya tiene reservada la estación.
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
                if (!station.HasPower)                    continue;
                if (station.IsOccupied && station.CurrentOccupant != pawn) continue;
                if (!pawn.CanReach(station, PathEndMode.InteractionCell, Danger.Deadly)) continue;

                var comp = station.GetComp<CompRobotRepairStation>();
                float maxRange = comp?.Props.maxRepairRange ?? 30f;

                float dist = pawn.Position.DistanceTo(station.Position);
                if (dist > maxRange) continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best     = station;
                }
            }

            return best;
        }

        public static CompRobotRepairStation FindBestRepairStationComp(Pawn pawn)
        {
            var station = FindBestRepairStation(pawn);
            return station?.GetComp<CompRobotRepairStation>();
        }
    }
}

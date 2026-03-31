using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    /// <summary>
    /// Think node que dispara cuando un mecanoid necesita reparación.
    /// Se añade via XML Patch al think tree de mecanoides.
    ///
    /// FIX D: Cachea el resultado de FindBestRepairStation en un campo de
    ///        instancia para que JobGiver_GoToRepairStation pueda reutilizarlo
    ///        sin hacer una segunda búsqueda completa (con CanReach/pathfinding)
    ///        en el mismo ciclo del think tree.
    /// </summary>
    public class ThinkNode_ConditionalNeedsRepair : ThinkNode_Conditional
    {
        // FIX D: cache de la station encontrada en Satisfied() para que
        // TryGiveJob() del nodo hijo la consuma sin buscar de nuevo.
        // Se invalida por pawn para evitar que un nodo compartido entre
        // distintos pawns devuelva una station errónea.
        [Unsaved]
        private Pawn lastEvaluatedPawn;
        [Unsaved]
        private Building_RobotRepairStation cachedStation;

        protected override bool Satisfied(Pawn pawn)
        {
            if (!pawn.RaceProps.IsMechanoid) return false;
            if (pawn.Faction != Faction.OfPlayer) return false;

            // FIX D: buscar una sola vez y cachear el resultado.
            var station = RepairStationUtility.FindBestRepairStation(pawn);
            lastEvaluatedPawn = pawn;
            cachedStation     = station;

            if (station == null) return false;

            var comp      = station.GetComp<CompRobotRepairStation>();
            float threshold = comp?.Props.repairHealthThreshold ?? 0.5f;

            return pawn.health.summaryHealth.SummaryHealthPercent < threshold;
        }

        /// <summary>
        /// Devuelve la station cacheada si corresponde al pawn indicado,
        /// evitando una segunda llamada a FindBestRepairStation.
        /// </summary>
        public Building_RobotRepairStation GetCachedStation(Pawn pawn)
            => lastEvaluatedPawn == pawn ? cachedStation : null;
    }

    /// <summary>
    /// Job giver: asigna el job GoToRepairStation al mecanoid.
    ///
    /// FIX D: Reutiliza la station cacheada por ThinkNode_ConditionalNeedsRepair
    ///        (nodo padre) en lugar de llamar FindBestRepairStation por segunda vez.
    /// FIX (previo #6): Verifica reservaciones antes de emitir el job para evitar
    ///        que dos mecanoides compitan por la misma station.
    /// </summary>
    public class JobGiver_GoToRepairStation : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            // FIX D: intentar reutilizar la station que Satisfied() ya encontró.
            var conditional = parent as ThinkNode_ConditionalNeedsRepair;
            var station = conditional?.GetCachedStation(pawn)
                       ?? RepairStationUtility.FindBestRepairStation(pawn);

            if (station == null) return null;

            // Verificar que la station no esté ya reservada por otro pawn.
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
    /// Utilidades para buscar y evaluar repair stations.
    ///
    /// FIX (previo #9): GetComp se llama exactamente una vez por station por búsqueda.
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

                // Una sola llamada GetComp por station; reutilizar para todos los lookups.
                var comp     = station.GetComp<CompRobotRepairStation>();
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
    }
}

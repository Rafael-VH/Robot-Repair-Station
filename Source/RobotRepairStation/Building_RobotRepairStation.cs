using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    /// <summary>
    /// The Robot Repair Station building. Manages which mechanoid is currently
    /// docked and broadcasts repair availability to nearby mechanoids.
    ///
    /// FIX A: SpawnSetup valida el job activo del ocupante tras cargar un save.
    ///        Si el pawn no tiene el job activo, currentOccupant se limpia para
    ///        evitar que la station quede bloqueada permanentemente.
    /// FIX B: TryConsumeSteel usa el ocupante actual como traverser en GenClosest
    ///        en lugar de TraverseParms.For(TraverseMode.PassDoors) sin pawn,
    ///        que producía comportamiento indefinido o NullReferenceException.
    /// FIX C: NotifyOccupantLeft() redirige a EjectOccupant() para garantizar
    ///        limpieza completa de reservaciones. Antes solo ponía currentOccupant
    ///        a null sin liberar la reserva, dejando un invisible lock.
    /// FIX F: TryConsumeSteel consume el stack de acero correctamente:
    ///        Destroy() directo si cogemos todo el stack, stackCount -= take si
    ///        cogemos parte. SplitOff().Destroy() sobre el Thing original cuando
    ///        take == stackCount no notificaba al sistema de haul.
    /// FIX H: GetInspectString usa Append + '\n' en lugar de AppendLine para
    ///        evitar el \r\n de Windows que produce doble salto en la UI de RimWorld.
    /// </summary>
    public class Building_RobotRepairStation : Building
    {
        // ─── State ────────────────────────────────────────────────────────────
        private Pawn currentOccupant;
        private int steelBuffer = 0;
        private const int SteelBufferMax = 50;

        // Cached comp reference
        private CompProperties_RobotRepairStation cachedCompProps;

        // ─── Properties ───────────────────────────────────────────────────────
        public CompProperties_RobotRepairStation RepairProps =>
            cachedCompProps ??= GetComp<CompRobotRepairStation>()?.Props;

        public bool IsOccupied => currentOccupant != null && !currentOccupant.Dead;

        public bool HasPower => this.TryGetComp<CompPowerTrader>()?.PowerOn ?? false;

        public bool HasSteel => steelBuffer > 0;

        public Pawn CurrentOccupant => currentOccupant;

        // ─── Spawning / Despawning ─────────────────────────────────────────────
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            RepairStationTracker.GetOrCreate(map).Register(this);

            // FIX A: Tras cargar un save, el pawn serializado puede no tener el
            // job activo (RimWorld no restaura JobDrivers automáticamente desde
            // referencias de building). Si currentOccupant no tiene el job,
            // limpiar el estado para evitar que la station quede bloqueada
            // permanentemente hasta que el jugador la destruya.
            if (respawningAfterLoad && currentOccupant != null)
            {
                bool hasActiveJob =
                    currentOccupant.CurJob?.def == RRS_JobDefOf.RRS_RepairAtStation ||
                    currentOccupant.CurJob?.def == RRS_JobDefOf.RRS_GoToRepairStation;

                if (!hasActiveJob)
                {
                    Log.Warning($"[RobotRepairStation] {currentOccupant.LabelShort} tenía" +
                                $" ocupada la station {Label} pero no tiene el job activo." +
                                " Limpiando estado para desbloquear la station.");
                    currentOccupant = null;
                }
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            RepairStationTracker.GetOrCreate(Map).Deregister(this);
            EjectOccupant();
            base.DeSpawn(mode);
        }

        // ─── Saving / Loading ─────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref currentOccupant, "currentOccupant");
            Scribe_Values.Look(ref steelBuffer, "steelBuffer", 0);
        }

        // ─── Tick ─────────────────────────────────────────────────────────────
        public override void Tick()
        {
            base.Tick();

            if (!HasPower) return;
            if (!IsOccupied) return;

            if (Find.TickManager.TicksGame % (RepairProps?.repairTickInterval ?? 500) == 0)
            {
                TryConsumeSteel();
            }
        }

        // ─── Occupant Management ─────────────────────────────────────────────
        public bool TryAcceptOccupant(Pawn mechanoid)
        {
            if (IsOccupied) return false;
            if (!HasPower) return false;

            currentOccupant = mechanoid;
            return true;
        }

        /// <summary>
        /// FIX C: Redirige a EjectOccupant() para garantizar limpieza completa
        /// (reservaciones incluidas). Antes este método solo hacía
        /// currentOccupant = null sin liberar la reserva del pawn, dejando
        /// un invisible lock que impedía a otros mecanoides reservar la station.
        /// </summary>
        public void NotifyOccupantLeft()
        {
            EjectOccupant();
        }

        /// <summary>
        /// Forces the current occupant to leave the station.
        /// Releases all reservations so the next mechanoid can reserve immediately.
        /// </summary>
        public void EjectOccupant()
        {
            if (!IsOccupied) return;

            Pawn occupant = currentOccupant;

            // End the repair job first (sets pawn free to receive new jobs).
            if (occupant.CurJob?.def == RRS_JobDefOf.RRS_RepairAtStation)
            {
                occupant.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            // Release any reservation this pawn still holds on this building.
            Map?.reservationManager.ReleaseAllForTarget(this);

            currentOccupant = null;
        }

        // ─── Steel Management ─────────────────────────────────────────────────
        private void TryConsumeSteel()
        {
            int toConsume = RepairProps?.steelPerRepairCycle ?? 1;

            if (steelBuffer >= toConsume)
            {
                steelBuffer -= toConsume;
                return;
            }

            // Buffer bajo — buscar acero en stockpiles / suelo cercanos.
            // FIX B: usar el ocupante como traverser para que PassDoors funcione
            // correctamente (necesita facción para saber qué puertas puede abrir).
            // Si por alguna razón no hay ocupante, caer a NoPassClosedDoors.
            TraverseParms traverseParams = currentOccupant != null
                ? TraverseParms.For(currentOccupant, Danger.Deadly)
                : TraverseParms.For(TraverseMode.NoPassClosedDoors);

            Thing steel = GenClosest.ClosestThingReachable(
                Position,
                Map,
                ThingRequest.ForDef(ThingDefOf.Steel),
                PathEndMode.ClosestTouch,
                traverseParams,
                searchRadius: 8f
            );

            if (steel != null)
            {
                int take = Mathf.Min(steel.stackCount, SteelBufferMax);

                // FIX F: consumir el stack correctamente para notificar al sistema
                // de haul. SplitOff().Destroy() sobre el Thing original cuando
                // take == stackCount no elimina el item del ListerHaulables.
                if (take >= steel.stackCount)
                    steel.Destroy(DestroyMode.Vanish);
                else
                    steel.stackCount -= take;

                // Rellenar buffer y descontar el ciclo actual.
                steelBuffer = Mathf.Max(0, take - toConsume);
            }
            else
            {
                // Sin acero — notificar al jugador y expulsar al ocupante.
                Messages.Message(
                    "RRS_LetterNoSteelText".Translate(currentOccupant.LabelShort),
                    this,
                    MessageTypeDefOf.NegativeEvent
                );
                EjectOccupant();
            }
        }

        // ─── Gizmos ───────────────────────────────────────────────────────────
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
                yield return g;

            if (IsOccupied)
            {
                yield return new Command_Action
                {
                    defaultLabel = "RRS_GizmoEjectOccupant".Translate(),
                    defaultDesc  = "RRS_GizmoEjectOccupantDesc".Translate(),
                    icon         = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport"),
                    action       = EjectOccupant
                };
            }
        }

        // ─── Inspection String ────────────────────────────────────────────────
        public override string GetInspectString()
        {
            // FIX H: Usar Append + '\n' en lugar de AppendLine.
            // AppendLine emite \r\n en Windows, lo que produce doble salto de
            // línea visible en el panel de inspección de RimWorld.
            var sb = new StringBuilder(base.GetInspectString());

            if (!HasPower)
            {
                sb.Append("RRS_InspectorNoPower".Translate()).Append('\n');
            }
            else if (IsOccupied)
            {
                sb.Append("RRS_InspectorCurrentOccupant".Translate(currentOccupant.LabelShort)).Append('\n');
                sb.Append($"Health: {(currentOccupant.health.summaryHealth.SummaryHealthPercent * 100f):F0}%").Append('\n');
                if (!HasSteel)
                    sb.Append("RRS_InspectorNoSteel".Translate()).Append('\n');
            }
            else
            {
                sb.Append("RRS_InspectorEmpty".Translate()).Append('\n');
            }

            sb.Append($"Steel buffer: {steelBuffer}/{SteelBufferMax}");

            return sb.ToString().TrimEndNewlines();
        }
    }
}

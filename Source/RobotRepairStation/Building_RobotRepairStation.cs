using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    public class Building_RobotRepairStation : Building
    {
        // ─── Estado ───────────────────────────────────────────────────────────
        private Pawn currentOccupant;
        private int steelBuffer = 0;
        private const int SteelBufferMax = 50;

        // Ref cacheada al comp para no llamar GetComp cada tick
        private CompProperties_RobotRepairStation cachedCompProps;

        // ─── Propiedades ──────────────────────────────────────────────────────
        public CompProperties_RobotRepairStation RepairProps =>
            cachedCompProps ?? (cachedCompProps = GetComp<CompRobotRepairStation>()?.Props);

        public bool IsOccupied  => currentOccupant != null && !currentOccupant.Dead;
        public bool HasPower    => this.TryGetComp<CompPowerTrader>()?.PowerOn ?? false;
        public bool HasSteel    => steelBuffer > 0;
        public Pawn CurrentOccupant => currentOccupant;

        // ─── Spawn / Despawn ──────────────────────────────────────────────────
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            RepairStationTracker.GetOrCreate(map).Register(this);

        }

        public override void PostMapInit()
        {
            base.PostMapInit();

            if (currentOccupant == null) return;

            bool hasActiveJob =
                currentOccupant.CurJob?.def == RRS_JobDefOf.RRS_RepairAtStation ||
                currentOccupant.CurJob?.def == RRS_JobDefOf.RRS_GoToRepairStation;

            if (!hasActiveJob)
            {
                Log.Warning(
                    $"[RobotRepairStation] {currentOccupant.LabelShort} estaba" +
                    $" registrado en {Label} pero no tiene el job de reparación activo." +
                    " Limpiando estado para desbloquear la estación.");
                currentOccupant = null;
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            RepairStationTracker.GetOrCreate(Map).Deregister(this);
            EjectOccupant();
            base.DeSpawn(mode);
        }

        // ─── Guardado / Carga ─────────────────────────────────────────────────
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref currentOccupant, "currentOccupant");
            Scribe_Values.Look(ref steelBuffer, "steelBuffer", 0);
        }

        // ─── Tick ─────────────────────────────────────────────────────────────
        protected override void Tick()
        {
            base.Tick();

            if (!HasPower)    return;
            if (!IsOccupied)  return;
            if (RepairProps == null) return;

            if (Find.TickManager.TicksGame % RepairProps.repairTickInterval == 0)
                TryConsumeSteel();
        }

        // ─── Gestión de ocupante ──────────────────────────────────────────────
        public bool TryAcceptOccupant(Pawn mechanoid)
        {
            if (IsOccupied)  return false;
            if (!HasPower)   return false;

            currentOccupant = mechanoid;
            return true;
        }

        /// <summary>
        /// Llamado por JobDriver_RepairAtStation cuando el driver termina por sí
        /// mismo (reparación completa). Solo limpia currentOccupant; el job ya
        /// terminó por su propia lógica, no hay que terminarlo desde aquí.
        /// </summary>
        public void NotifyOccupantLeft()
        {
            currentOccupant = null;
        }

        /// <summary>
        /// Fuerza la expulsión del ocupante actual. Usado por: sin acero, sin
        /// potencia, destrucción del edificio, gizmo de expulsión manual.
        /// </summary>
        public void EjectOccupant()
        {
            if (!IsOccupied) return;

            Pawn occupant = currentOccupant;
            currentOccupant = null;

            Job jobToRelease = occupant.CurJob;

            if (occupant.CurJob?.def == RRS_JobDefOf.RRS_RepairAtStation)
                occupant.jobs.EndCurrentJob(JobCondition.InterruptForced);

            // Liberar la reserva usando el job capturado antes del EndCurrentJob.
            if (jobToRelease != null)
                Map?.reservationManager.Release(this, occupant, jobToRelease);
        }

        // ─── Consumo de acero ─────────────────────────────────────────────────
        private void TryConsumeSteel()
        {
            int toConsume = RepairProps?.steelPerRepairCycle ?? 1;

            // El path "suficiente" no descontaba nada, regalando 1 acero por ciclo.
            if (steelBuffer >= toConsume)
            {
                steelBuffer -= toConsume;   // consumo normal del buffer
                return;
            }

            // Buffer insuficiente: buscar acero cercano.
            TraverseParms traverseParams = currentOccupant != null
                ? TraverseParms.For(currentOccupant, Danger.Deadly)
                : TraverseParms.For(TraverseMode.NoPassClosedDoors);

            Thing steel = GenClosest.ClosestThingReachable(
                Position,
                Map,
                ThingRequest.ForDef(ThingDefOf.Steel),
                PathEndMode.ClosestTouch,
                traverseParams,
                maxDistance: 8f
            );

            if (steel != null)
            {
                int take = Mathf.Min(steel.stackCount, SteelBufferMax);

                if (take >= steel.stackCount)
                    steel.Destroy(DestroyMode.Vanish);
                else
                    steel.stackCount -= take;

                // Recargar buffer y descontar el consumo de este ciclo.
                // steelBuffer = take - toConsume es correcto (take >= 1 siempre
                // porque Mathf.Min devuelve al menos 1 si stackCount > 0).
                steelBuffer = Mathf.Max(0, take - toConsume);
            }
            else
            {
                Messages.Message(
                    "RRS_LetterNoSteelText".Translate(currentOccupant?.LabelShort ?? "mechanoid"),
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

        // ─── Panel de inspección ──────────────────────────────────────────────
        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (!HasPower)
            {
                sb.AppendLine("RRS_InspectorNoPower".Translate());
            }
            else if (IsOccupied)
            {
                sb.AppendLine("RRS_InspectorCurrentOccupant".Translate(currentOccupant.LabelShort));
                sb.AppendLine($"Health: {currentOccupant.health.summaryHealth.SummaryHealthPercent * 100f:F0}%");
                if (!HasSteel)
                    sb.AppendLine("RRS_InspectorNoSteel".Translate());
            }
            else
            {
                sb.AppendLine("RRS_InspectorEmpty".Translate());
            }

            sb.Append($"Steel buffer: {steelBuffer}/{SteelBufferMax}");
            return sb.ToString().TrimEndNewlines();
        }
    }
}

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
            if (RepairProps == null) return; // guarda adicional

            if (Find.TickManager.TicksGame % RepairProps.repairTickInterval == 0)

                TryConsumeSteel();

        }

        // ─── Occupant Management ─────────────────────────────────────────────
        public bool TryAcceptOccupant(Pawn mechanoid)
        {
            if (IsOccupied) return false;
            if (!HasPower) return false;

            currentOccupant = mechanoid;
            return true;
        }

        public void NotifyOccupantLeft()
        {
            EjectOccupant();
        }

        public void EjectOccupant()
        {
            if (!IsOccupied) return;

            Pawn occupant = currentOccupant;

            if (occupant.CurJob?.def == RRS_JobDefOf.RRS_RepairAtStation)
            {
                occupant.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            Map?.reservationManager.Release(this, occupant, occupant.CurJob);

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

                if (take >= steel.stackCount)
                    steel.Destroy(DestroyMode.Vanish);
                else
                    steel.stackCount -= take;

                steelBuffer = Mathf.Max(0, take - toConsume);
            }
            else
            {
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
                    defaultDesc = "RRS_GizmoEjectOccupantDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport"),
                    action = EjectOccupant
                };
            }
        }

        // ─── Inspection String ────────────────────────────────────────────────
        public override string GetInspectString()
        {
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

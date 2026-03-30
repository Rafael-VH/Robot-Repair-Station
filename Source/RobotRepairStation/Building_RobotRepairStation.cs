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
            // Register with the repair station tracker for this map
            RepairStationTracker.GetOrCreate(map).Register(this);
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

            // Try to consume steel periodically
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

        public void NotifyOccupantLeft()
        {
            currentOccupant = null;
        }

        public void EjectOccupant()
        {
            if (!IsOccupied) return;

            if (currentOccupant.CurJob?.def == RRS_JobDefOf.RRS_RepairAtStation)
            {
                currentOccupant.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            currentOccupant = null;
        }

        // ─── Steel Management ─────────────────────────────────────────────────
        private void TryConsumeSteel()
        {
            if (steelBuffer > 0)
            {
                steelBuffer--;
                return;
            }

            // Try to pull steel from adjacent stockpiles / ground
            Thing steel = GenClosest.ClosestThingReachable(
                Position,
                Map,
                ThingRequest.ForDef(ThingDefOf.Steel),
                PathEndMode.ClosestTouch,
                TraverseParms.For(TraverseMode.PassDoors),
                searchRadius: 8f
            );

            if (steel != null)
            {
                int take = Mathf.Min(steel.stackCount, SteelBufferMax);
                steel.SplitOff(take).Destroy();
                steelBuffer = take - 1;
            }
            else
            {
                // No steel found — notify and eject
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
            foreach (var g in base.GetGizmos())
                yield return g;

            // Eject button
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
            var sb = new StringBuilder(base.GetInspectString());

            if (!HasPower)
            {
                sb.AppendLine("RRS_InspectorNoPower".Translate());
            }
            else if (IsOccupied)
            {
                sb.AppendLine("RRS_InspectorCurrentOccupant".Translate(currentOccupant.LabelShort));
                sb.AppendLine($"Health: {(currentOccupant.health.summaryHealth.SummaryHealthPercent * 100f):F0}%");
                if (!HasSteel)
                    sb.AppendLine("RRS_InspectorNoSteel".Translate());
            }
            else
            {
                sb.AppendLine("RRS_InspectorEmpty".Translate());
            }

            sb.AppendLine($"Steel buffer: {steelBuffer}/{SteelBufferMax}");

            return sb.ToString().TrimEndNewlines();
        }
    }
}

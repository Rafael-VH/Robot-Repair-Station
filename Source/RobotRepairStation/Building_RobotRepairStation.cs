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
    /// FIX #4: TryConsumeSteel now subtracts Props.steelPerRepairCycle from the
    ///         buffer instead of always decrementing by 1.
    /// FIX #5: EjectOccupant releases all reservations the occupant held on this
    ///         building so other mechanoids can reserve it immediately.
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

        public void NotifyOccupantLeft()
        {
            currentOccupant = null;
        }

        /// <summary>
        /// Forces the current occupant to leave the station.
        ///
        /// FIX #5: After ending the occupant's job, all reservations that pawn
        /// held on this building are released so the next mechanoid can
        /// immediately reserve and path to the station.
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

            // FIX #5: Release any reservation this pawn (or any pawn of its
            // faction) still holds on this building. Without this the station
            // appears reserved and other mechanoids cannot be assigned to it.
            Map?.reservationManager.ReleaseAllForTarget(this);

            currentOccupant = null;
        }

        // ─── Steel Management ─────────────────────────────────────────────────
        private void TryConsumeSteel()
        {
            // FIX #4: Consume steelPerRepairCycle units per interval, not always 1.
            int toConsume = RepairProps?.steelPerRepairCycle ?? 1;

            if (steelBuffer >= toConsume)
            {
                steelBuffer -= toConsume;
                return;
            }

            // Buffer is too low — try to pull steel from adjacent stockpiles / ground.
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

                // FIX #4: Fill buffer then immediately deduct this cycle's cost.
                steelBuffer = take - toConsume;
                steelBuffer = Mathf.Max(steelBuffer, 0);
            }
            else
            {
                // No steel found — notify player and eject the occupant.
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

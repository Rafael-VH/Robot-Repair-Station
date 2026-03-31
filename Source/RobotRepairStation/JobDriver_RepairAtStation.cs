using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    /// <summary>
    /// Job driver: the mechanoid stays docked at the station while being repaired.
    /// The actual healing is handled by CompRobotRepairStation.CompTick().
    /// This driver keeps the pawn in place and handles interruptions.
    ///
    /// FIX #2: tickAction no longer checks health percentage directly.
    ///         The completion path is:
    ///           CompRobotRepairStation.ApplyRepairTick() detects ≥ 99%
    ///           → OnRepairComplete() → Station.EjectOccupant()
    ///           → Station.CurrentOccupant becomes null
    ///           → tickAction detects CurrentOccupant != pawn → EndJobWith(Succeeded)
    ///         This eliminates the race condition where both the Comp and the
    ///         driver independently tried to eject/end at the same tick.
    /// </summary>
    public class JobDriver_RepairAtStation : JobDriver
    {
        private Building_RobotRepairStation Station =>
            (Building_RobotRepairStation)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // The reservation was already made by JobDriver_GoToRepairStation.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Structural fail conditions (station destroyed, power lost, etc.)
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Station.HasPower);

            // Wait at interaction cell indefinitely.
            // Healing happens in CompRobotRepairStation.CompTick().
            var wait = new Toil();

            wait.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.rotationTracker.FaceTarget(Station);
            };

            wait.tickAction = () =>
            {
                // FIX #2: Do NOT check health here.
                // The Comp is the single source of truth for repair completion.
                // When the Comp calls EjectOccupant(), CurrentOccupant is set to null.
                // We detect that here and exit cleanly — no double-eject possible.
                if (Station.CurrentOccupant != pawn)
                {
                    EndJobWith(JobCondition.Succeeded);
                }
            };

            wait.handlingFacing = true;
            wait.defaultCompleteMode = ToilCompleteMode.Never;

            yield return wait;
        }

        public override void Notify_DamageTaken(DamageInfo dinfo)
        {
            base.Notify_DamageTaken(dinfo);
            // Do not interrupt repair on damage — mechanoid stays docked.
        }

        public override bool IsContinuation(Job j)
        {
            return j.def == RRS_JobDefOf.RRS_GoToRepairStation
                && j.targetA == job.targetA;
        }
    }
}

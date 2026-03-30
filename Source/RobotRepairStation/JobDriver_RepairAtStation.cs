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
    /// </summary>
    public class JobDriver_RepairAtStation : JobDriver
    {
        private Building_RobotRepairStation Station =>
            (Building_RobotRepairStation)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Already reserved by the GoTo job
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail conditions
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Station.HasPower);
            this.FailOn(() => Station.CurrentOccupant != pawn);

            // Wait at interaction cell indefinitely (healing happens in CompTick)
            var wait = new Toil();
            wait.initAction = () =>
            {
                pawn.pather.StopDead();
            };
            wait.tickAction = () =>
            {
                // Check if fully healed → job ends naturally via CompRobotRepairStation
                if (pawn.health.summaryHealth.SummaryHealthPercent >= 0.99f)
                {
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            wait.handlingFacing = true;
            wait.defaultCompleteMode = ToilCompleteMode.Never;

            // Face the station while repairing
            wait.initAction += () =>
            {
                pawn.rotationTracker.FaceTarget(Station);
            };

            yield return wait;
        }

        public override void Notify_DamageTaken(DamageInfo dinfo)
        {
            base.Notify_DamageTaken(dinfo);
            // Do not interrupt repair on damage (mechanoid is being repaired, not fleeing)
        }

        public override bool IsContinuation(Job j)
        {
            return j.def == RRS_JobDefOf.RRS_GoToRepairStation && j.targetA == job.targetA;
        }
    }
}

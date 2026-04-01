using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    public class JobDriver_RepairAtStation : JobDriver
    {
        private Building_RobotRepairStation Station =>
            (Building_RobotRepairStation)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Station.HasPower);

            var wait = new Toil();

            wait.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.rotationTracker.FaceTarget(Station);
            };

            wait.tickAction = () =>
            {
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
        }

        public override bool IsContinuation(Job j)
        {
            return j.def == RRS_JobDefOf.RRS_GoToRepairStation
                && j.targetA == job.targetA;
        }
    }
}

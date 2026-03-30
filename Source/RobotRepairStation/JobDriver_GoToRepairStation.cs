using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    /// <summary>
    /// Job driver: the mechanoid walks to the repair station and docks.
    /// Transitions to RRS_RepairAtStation once it arrives.
    /// </summary>
    public class JobDriver_GoToRepairStation : JobDriver
    {
        private Building_RobotRepairStation Station =>
            (Building_RobotRepairStation)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail if station becomes invalid
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.A);
            this.FailOn(() => !Station.HasPower);
            this.FailOn(() => Station.IsOccupied && Station.CurrentOccupant != pawn);

            // Walk to the station
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Dock and begin repair job
            var dock = new Toil();
            dock.initAction = () =>
            {
                if (Station.TryAcceptOccupant(pawn))
                {
                    var repairJob = JobMaker.MakeJob(RRS_JobDefOf.RRS_RepairAtStation, Station);
                    pawn.jobs.StartJob(repairJob, JobCondition.Succeeded);
                }
                else
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            dock.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return dock;
        }
    }
}

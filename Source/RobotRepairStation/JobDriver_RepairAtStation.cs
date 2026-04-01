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
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
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
                // El Building ha puesto currentOccupant = null (vía NotifyOccupantLeft
                // cuando la reparación completó, o vía EjectOccupant cuando se quedó
                // sin acero). El driver lo detecta aquí y finaliza limpiamente.
                if (Station.CurrentOccupant != pawn)
                    EndJobWith(JobCondition.Succeeded);
            };

            wait.handlingFacing    = true;
            wait.defaultCompleteMode = ToilCompleteMode.Never;

            yield return wait;
        }

        public override void Notify_DamageTaken(DamageInfo dinfo)
        {
            base.Notify_DamageTaken(dinfo);
            // No interrumpir por daño: checkOverrideOnDamage=false en el JobDef
            // ya lo gestiona a nivel de JobDef; este override queda vacío intencionalmente.
        }

        public override bool IsContinuation(Job j)
        {
            return j.def == RRS_JobDefOf.RRS_GoToRepairStation
                && j.targetA == job.targetA;
        }
    }
}

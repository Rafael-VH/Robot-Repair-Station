using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    /// <summary>
    /// Driver del job <see cref="RRS_JobDefOf.RRS_RepairAtStation"/>.
    /// <para>
    /// Responsabilidad: mantener al mecanoid quieto en la estación mientras
    /// <see cref="CompRobotRepairStation"/> aplica la curación tick a tick.
    /// Este driver no hace nada activo — simplemente espera hasta que
    /// el Building o el Comp señalen que debe terminar.
    /// </para>
    /// <para>
    /// Mecanismo de terminación (señal de fin):
    /// El driver monitoriza <see cref="Building_RobotRepairStation.CurrentOccupant"/>
    /// en su <c>tickAction</c>. Cuando dicho campo se pone a <c>null</c> — ya sea
    /// porque la reparación completó (<see cref="Building_RobotRepairStation.NotifyOccupantLeft"/>)
    /// o porque se forzó la expulsión (<see cref="Building_RobotRepairStation.EjectOccupant"/>) —
    /// el driver llama a <c>EndJobWith(Succeeded)</c>.
    /// </para>
    /// </summary>
    public class JobDriver_RepairAtStation : JobDriver
    {
        // ─── Acceso tipado al objetivo ────────────────────────────────────────

        /// <summary>
        /// Referencia a la estación objetivo del job (almacenada en <c>job.targetA</c>).
        /// </summary>
        private Building_RobotRepairStation Station =>
            (Building_RobotRepairStation)job.targetA.Thing;

        // ─── Reservas ─────────────────────────────────────────────────────────

        /// <summary>
        /// Reserva la estación para este pawn.
        /// <para>
        /// La reserva garantiza que, mientras el mecanoid está siendo reparado,
        /// ningún otro pawn puede reservar la misma estación. Capacidad 1 (una
        /// reserva por edificio).
        /// </para>
        /// </summary>
        /// <param name="errorOnFailed">Si <c>true</c>, registrar error en caso de fallo.</param>
        /// <returns><c>true</c> si la reserva tuvo éxito.</returns>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        // ─── Toils ────────────────────────────────────────────────────────────

        /// <summary>
        /// Define la secuencia de pasos de este job: un único toil de espera
        /// indefinida (<c>ToilCompleteMode.Never</c>) que termina cuando el
        /// edificio o el comp señalan fin de ocupación.
        /// <para>
        /// Condición de fallo global:
        /// <list type="bullet">
        ///   <item>La estación es destruida o nula.</item>
        ///   <item>La estación pierde energía (el Building llamará a EjectOccupant).</item>
        /// </list>
        /// La pérdida de acero no termina el job aquí directamente; es el Building
        /// quien llama a <see cref="Building_RobotRepairStation.EjectOccupant"/>,
        /// que pone <c>CurrentOccupant = null</c>, señal que el <c>tickAction</c>
        /// detecta.
        /// </para>
        /// </summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fallo si la estación desaparece o pierde energía.
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Station.HasPower);

            var wait = new Toil();

            // initAction: detener al pawn en su lugar y orientarlo hacia la estación
            // para dar feedback visual de que está siendo reparado.
            wait.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.rotationTracker.FaceTarget(Station);
            };

            // tickAction: monitorizar si el Building puso CurrentOccupant a null.
            // Esto ocurre en dos escenarios:
            //   1. Reparación completa → NotifyOccupantLeft() → null.
            //   2. Expulsión forzada   → EjectOccupant()      → null.
            // En ambos casos, terminar limpiamente con Succeeded.
            wait.tickAction = () =>
            {
                if (Station.CurrentOccupant != pawn)
                    EndJobWith(JobCondition.Succeeded);
            };

            wait.handlingFacing      = true;          // El toil gestiona la rotación manualmente.
            wait.defaultCompleteMode = ToilCompleteMode.Never; // Sin timeout, espera indefinida.

            yield return wait;
        }

        // ─── Overrides de comportamiento ──────────────────────────────────────

        /// <summary>
        /// Llamado cuando el pawn recibe daño mientras ejecuta este job.
        /// <para>
        /// Override intencional vacío: la interrupción por daño ya está desactivada
        /// a nivel de <see cref="JobDef"/> mediante <c>checkOverrideOnDamage=false</c>.
        /// Este override existe como documentación explícita de que la decisión fue
        /// consciente, no un olvido.
        /// </para>
        /// </summary>
        /// <param name="dinfo">Información del daño recibido.</param>
        public override void Notify_DamageTaken(DamageInfo dinfo)
        {
            base.Notify_DamageTaken(dinfo);
            // Vacío a propósito: no interrumpir la reparación por daño entrante.
            // checkOverrideOnDamage=false en el JobDef gestiona esto a nivel de scheduler.
        }

        /// <summary>
        /// Determina si este job es una continuación directa del job de navegación
        /// (<see cref="RRS_JobDefOf.RRS_GoToRepairStation"/>) hacia la misma estación.
        /// <para>
        /// RimWorld usa este método para decidir si, al encolar este job inmediatamente
        /// después del job de navegación, puede omitir la fase de reservas sin causar
        /// conflictos. Devolver <c>true</c> indica que los dos jobs son parte del
        /// mismo flujo y comparten el mismo objetivo.
        /// </para>
        /// </summary>
        /// <param name="j">El job previo que se está comparando.</param>
        /// <returns>
        /// <c>true</c> si <paramref name="j"/> es el job de navegación hacia esta misma estación.
        /// </returns>
        public override bool IsContinuation(Job j)
        {
            return j.def == RRS_JobDefOf.RRS_GoToRepairStation
                && j.targetA == job.targetA;
        }
    }
}

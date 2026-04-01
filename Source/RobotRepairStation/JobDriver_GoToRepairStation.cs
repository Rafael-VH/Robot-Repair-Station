using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    /// <summary>
    /// Driver del job <see cref="RRS_JobDefOf.RRS_GoToRepairStation"/>.
    /// <para>
    /// Responsabilidad: desplazar al mecanoid desde su posición actual hasta la
    /// celda de interacción de la <see cref="Building_RobotRepairStation"/> y,
    /// una vez llegado, intentar registrarse como ocupante y encolar el job de
    /// reparación (<see cref="RRS_JobDefOf.RRS_RepairAtStation"/>).
    /// </para>
    /// <para>
    /// Flujo de toils:
    /// <list type="number">
    ///   <item><b>GotoThing</b> — el pawn camina hasta la celda de interacción.</item>
    ///   <item>
    ///     <b>dock (Instant)</b> — intenta ocupar la estación; si tiene éxito,
    ///     encola <see cref="RRS_JobDefOf.RRS_RepairAtStation"/> y termina con
    ///     <c>Succeeded</c>. Si falla (estación ocupada o sin energía), termina
    ///     con <c>Incompletable</c>.
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    public class JobDriver_GoToRepairStation : JobDriver
    {
        // ─── Acceso tipado al objetivo ────────────────────────────────────────

        /// <summary>
        /// Referencia a la estación objetivo del job (almacenada en <c>job.targetA</c>).
        /// </summary>
        private Building_RobotRepairStation Station =>
            (Building_RobotRepairStation)job.targetA.Thing;

        // ─── Reservas ─────────────────────────────────────────────────────────

        /// <summary>
        /// Llamado por RimWorld antes de iniciar el job para reservar los objetivos.
        /// <para>
        /// Reserva la estación con capacidad 1 (solo un pawn puede tenerla reservada
        /// a la vez). Si la reserva falla y <paramref name="errorOnFailed"/> es
        /// <c>true</c>, RimWorld registra un error en el log.
        /// </para>
        /// </summary>
        /// <param name="errorOnFailed">Si <c>true</c>, registrar error en caso de fallo.</param>
        /// <returns><c>true</c> si la reserva tuvo éxito; <c>false</c> en caso contrario.</returns>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        // ─── Toils ────────────────────────────────────────────────────────────

        /// <summary>
        /// Define la secuencia de pasos (toils) que componen este job.
        /// <para>
        /// Condiciones de fallo globales (se evalúan cada tick para cualquier toil):
        /// <list type="bullet">
        ///   <item>La estación es destruida o nula (<c>FailOnDespawnedOrNull</c>).</item>
        ///   <item>La estación está marcada como forbid (<c>FailOnForbidden</c>).</item>
        ///   <item>La estación pierde energía.</item>
        ///   <item>La estación es ocupada por otro pawn mientras navegamos.</item>
        /// </list>
        /// </para>
        /// </summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Condiciones de fallo que se evalúan automáticamente cada tick.
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.A);
            this.FailOn(() => !Station.HasPower);
            this.FailOn(() => Station.IsOccupied && Station.CurrentOccupant != pawn);

            // Toil 1: Mover al pawn hasta la celda de interacción de la estación.
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            // Toil 2: Intentar ocupar la estación (Instant = se completa en el mismo tick).
            var dock = new Toil();
            dock.initAction = () =>
            {
                if (Station.TryAcceptOccupant(pawn))
                {
                    // Ocupante registrado correctamente → encolar el job de reparación
                    // y terminar este job con éxito.
                    var repairJob = JobMaker.MakeJob(RRS_JobDefOf.RRS_RepairAtStation, Station);
                    pawn.jobs.jobQueue.EnqueueFirst(repairJob);
                    EndJobWith(JobCondition.Succeeded);
                }
                else
                {
                    // La estación fue ocupada por otro pawn entre la llegada y el dock
                    // (race condition) → terminar con fallo para que la IA lo reintente.
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            dock.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return dock;
        }
    }
}

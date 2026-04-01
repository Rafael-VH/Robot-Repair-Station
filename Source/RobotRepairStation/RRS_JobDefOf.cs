using RimWorld;
using Verse;

namespace RobotRepairStation
{
    /// <summary>
    /// Registro estático de los <see cref="JobDef"/>s propios del mod.
    /// <para>
    /// El atributo <c>[DefOf]</c> hace que RimWorld inyecte automáticamente
    /// las referencias a las <see cref="JobDef"/>s después de que todas las
    /// definiciones XML han sido cargadas. Esto evita tener que buscar los
    /// <c>Def</c>s manualmente con <c>DefDatabase&lt;JobDef&gt;.GetNamed(...)</c>.
    /// </para>
    /// <para>
    /// Las <see cref="JobDef"/>s correspondientes se declaran en
    /// <c>Defs/JobDefs/JobDefs_RobotRepair.xml</c>.
    /// </para>
    /// </summary>
    [DefOf]
    public static class RRS_JobDefOf
    {
        // ─── Definiciones de Jobs ─────────────────────────────────────────────

        /// <summary>
        /// Job de navegación: el mecanoid se desplaza desde su posición actual
        /// hasta la celda de interacción (<c>InteractionCell</c>) de la
        /// <see cref="Building_RobotRepairStation"/> asignada.
        /// <para>
        /// Este job es emitido por <see cref="JobGiver_GoToRepairStation"/> y ejecutado
        /// por <see cref="JobDriver_GoToRepairStation"/>. Una vez que el mecanoid
        /// llega a la estación, el driver encola <see cref="RRS_RepairAtStation"/>
        /// y termina con <c>JobCondition.Succeeded</c>.
        /// </para>
        /// </summary>
        public static JobDef RRS_GoToRepairStation;

        /// <summary>
        /// Job de reparación: el mecanoid permanece en la estación mientras
        /// <see cref="CompRobotRepairStation"/> aplica curación tick a tick.
        /// <para>
        /// Este job es encolado por <see cref="JobDriver_GoToRepairStation"/> y
        /// ejecutado por <see cref="JobDriver_RepairAtStation"/>. Termina cuando
        /// <see cref="Building_RobotRepairStation.NotifyOccupantLeft"/> o
        /// <see cref="Building_RobotRepairStation.EjectOccupant"/> ponen
        /// <c>CurrentOccupant</c> a <c>null</c>, señal que el driver detecta
        /// en su <c>tickAction</c>.
        /// </para>
        /// </summary>
        public static JobDef RRS_RepairAtStation;

        // ─── Constructor estático ─────────────────────────────────────────────
        /// <summary>
        /// Constructor estático requerido por el patrón <c>[DefOf]</c> de RimWorld.
        /// <para>
        /// La llamada a <see cref="DefOfHelper.EnsureInitializedInCtor"/> garantiza
        /// que los campos se inicializan correctamente incluso cuando otra clase
        /// accede a este tipo antes de que RimWorld haya terminado la inyección
        /// automática de <c>Def</c>s.
        /// </para>
        /// </summary>
        static RRS_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RRS_JobDefOf));
        }
    }
}

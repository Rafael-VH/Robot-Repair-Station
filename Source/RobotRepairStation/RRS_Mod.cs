using Verse;

namespace RobotRepairStation
{
    /// <summary>
    /// Punto de entrada del mod Robot Repair Station.
    /// <para>
    /// El atributo <c>[StaticConstructorOnStartup]</c> garantiza que el bloque estático
    /// se ejecuta una sola vez cuando RimWorld termina de cargar todos los mods,
    /// justo después de que se resuelvan todas las <c>Def</c>s y se apliquen los patches XML.
    /// </para>
    /// <para>
    /// Actualmente la integración con el ThinkTree de mecanoides se realiza
    /// completamente por XML (<c>Patches/MechanoidThinkTree.xml</c>), por lo que
    /// no se registran patches de Harmony aquí. Si en el futuro se necesitan
    /// patches en runtime (p. ej. para interceptar métodos privados de RimWorld),
    /// este es el lugar correcto para inicializarlos con
    /// <c>new Harmony("RexThar.RobotRepairStation").PatchAll()</c>.
    /// </para>
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RRS_Mod
    {
        // ─── Constructor estático ─────────────────────────────────────────────
        /// <summary>
        /// Constructor estático invocado automáticamente por el runtime de RimWorld
        /// al finalizar la carga de todos los mods.
        /// <para>
        /// Registra un mensaje en el log del juego para confirmar que el mod
        /// se cargó correctamente. Este mensaje es visible en el DevLog de RimWorld
        /// (<c>Options → Open debug log</c>) y ayuda a diagnosticar problemas de carga.
        /// </para>
        /// </summary>
        static RRS_Mod()
        {
            Log.Message("[RobotRepairStation] Mod cargado correctamente.");
        }
    }
}

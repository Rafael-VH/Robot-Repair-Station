using Verse;

namespace RobotRepairStation
{
    // StaticConstructorOnStartup garantiza que este bloque se ejecuta
    // cuando RimWorld termina de cargar todos los mods.
    // Actualmente no se usan patches de Harmony — la integración con el
    // ThinkTree de mecanoides se realiza completamente por XML en
    // Patches/MechanoidThinkTree.xml.
    [StaticConstructorOnStartup]
    public static class RRS_Mod
    {
        static RRS_Mod()
        {
            Log.Message("[RobotRepairStation] Mod cargado correctamente.");
        }
    }
}

using Verse;

namespace RobotRepairStation
{
    [StaticConstructorOnStartup]
    public static class RRS_Mod
    {
        static RRS_Mod()
        {
            var harmony = new Harmony("TuNombre.RobotRepairStation");
            harmony.PatchAll();
            Log.Message("[RobotRepairStation] Harmony patches applied.");
        }
    }

    public static class Patch_PawnJobTracker_DetermineNextJob
    {
        // No patch methods — see Patches/MechanoidThinkTree.xml.
    }
}

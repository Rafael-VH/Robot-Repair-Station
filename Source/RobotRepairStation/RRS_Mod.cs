using HarmonyLib;
using Verse;

namespace RobotRepairStation
{
    /// <summary>
    /// Entry point for the mod. Applies Harmony patches on startup.
    ///
    /// FIX #10: Removed the empty [HarmonyPatch] attribute from the documentation
    ///          class below. Harmony 2.x would attempt to patch
    ///          Pawn_JobTracker.DetermineNextJob with no prefix/postfix, which
    ///          produces log warnings on some builds and wastes a patch slot.
    ///          The comment is kept for documentation purposes only.
    /// </summary>
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

    /// <summary>
    /// Documentation stub — no Harmony patch is applied here.
    ///
    /// The mechanoid AI injection is handled entirely via the XML patch at
    /// Patches/MechanoidThinkTree.xml, which inserts
    /// ThinkNode_ConditionalNeedsRepair before the vanilla charge subtree.
    ///
    /// If a runtime Harmony patch is ever needed in the future, add the
    /// [HarmonyPatch] attribute back to this class and implement the
    /// appropriate prefix / postfix / transpiler static methods.
    /// </summary>
    public static class Patch_PawnJobTracker_DetermineNextJob
    {
        // No patch methods — see Patches/MechanoidThinkTree.xml.
    }
}

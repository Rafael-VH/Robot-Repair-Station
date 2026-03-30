using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    /// <summary>
    /// Entry point for the mod. Applies Harmony patches on startup.
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
    /// Patches the mechanoid think tree resolver to inject the repair need node
    /// at a high priority so mechanoids seek repair before other low-priority tasks.
    ///
    /// We hook into ThinkTreeDef.DoResolveReferences because that is where
    /// the tree nodes are fully assembled and ready to be modified.
    ///
    /// Alternative: use an XML Patch on the MechanoidConstant ThinkTree def
    /// (see Patches/MechanoidThinkTree.xml for the XML-only approach).
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    public static class Patch_PawnJobTracker_DetermineNextJob
    {
        // No prefix/postfix here; we rely on the ThinkTree XML patch instead.
        // This file is kept as the Harmony bootstrap entry point only.
    }
}

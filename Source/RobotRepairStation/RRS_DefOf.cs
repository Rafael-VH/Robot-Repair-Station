using RimWorld;
using Verse;

namespace RobotRepairStation
{
    /// <summary>
    /// Static references to JobDefs defined in XML.
    /// RimWorld automatically populates these via [DefOf] attribute.
    /// </summary>
    [DefOf]
    public static class RRS_JobDefOf
    {
        public static JobDef RRS_GoToRepairStation;
        public static JobDef RRS_RepairAtStation;

        static RRS_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RRS_JobDefOf));
        }
    }
}

using RimWorld;
using Verse;

namespace RobotRepairStation
{
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

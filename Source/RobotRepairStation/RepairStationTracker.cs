using System.Collections.Generic;
using Verse;

namespace RobotRepairStation
{
    /// <summary>
    /// MapComponent that keeps a registry of all active Robot Repair Stations.
    /// Used by the AI to quickly find nearby stations without iterating all things.
    /// </summary>
    public class RepairStationTracker : MapComponent
    {
        private readonly List<Building_RobotRepairStation> stations =
            new List<Building_RobotRepairStation>();

        public IReadOnlyList<Building_RobotRepairStation> AllStations => stations;

        public RepairStationTracker(Map map) : base(map) { }

        public static RepairStationTracker GetOrCreate(Map map)
        {
            var tracker = map.GetComponent<RepairStationTracker>();
            if (tracker == null)
            {
                tracker = new RepairStationTracker(map);
                map.components.Add(tracker);
            }
            return tracker;
        }

        public void Register(Building_RobotRepairStation station)
        {
            if (!stations.Contains(station))
                stations.Add(station);
        }

        public void Deregister(Building_RobotRepairStation station)
        {
            stations.Remove(station);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // Stations re-register themselves on spawn after load, so no need to save the list.
        }
    }
}

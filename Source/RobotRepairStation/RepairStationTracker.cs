using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RobotRepairStation
{
    public class RepairStationTracker : MapComponent
    {
        private readonly List<Building_RobotRepairStation> stations =
            new List<Building_RobotRepairStation>();

        public IReadOnlyList<Building_RobotRepairStation> AllStations => stations;

        public RepairStationTracker(Map map) : base(map) { }

        public static RepairStationTracker GetOrCreate(Map map)
        {
            var existing = map.components.OfType<RepairStationTracker>().FirstOrDefault();
            if (existing != null) return existing;

            var tracker = new RepairStationTracker(map);
            map.components.Add(tracker);
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
        }
    }
}

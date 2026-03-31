using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RobotRepairStation
{
    /// <summary>
    /// MapComponent que mantiene un registro de todas las Robot Repair Stations activas.
    /// Usado por la IA para encontrar stations cercanas sin iterar todos los Things del mapa.
    ///
    /// FIX G: GetOrCreate usa OfType&lt;RepairStationTracker&gt;().FirstOrDefault() en lugar
    ///        de map.GetComponent&lt;T&gt;() para evitar crear un tracker duplicado vacío
    ///        cuando el componente ya existe serializado en map.components pero
    ///        GetComponent no lo encuentra por posición en la lista.
    ///        Un tracker duplicado vacío ocultaba al original con las stations
    ///        registradas, haciendo que FindBestRepairStation devolviera siempre null.
    /// </summary>
    public class RepairStationTracker : MapComponent
    {
        private readonly List<Building_RobotRepairStation> stations =
            new List<Building_RobotRepairStation>();

        public IReadOnlyList<Building_RobotRepairStation> AllStations => stations;

        public RepairStationTracker(Map map) : base(map) { }

        /// <summary>
        /// FIX G: Busca en toda la lista map.components mediante OfType para
        /// garantizar que solo existe una instancia del tracker por mapa.
        /// GetComponent&lt;T&gt; devuelve el primero encontrado pero puede fallar si
        /// el orden de la lista cambia por carga corrupta u otros mods.
        /// </summary>
        public static RepairStationTracker GetOrCreate(Map map)
        {
            // OfType es más robusto que GetComponent: no depende del orden de la lista
            // y detecta instancias duplicadas antes de crear una nueva.
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
            // Las stations se re-registran solas en SpawnSetup al cargar,
            // por lo que no es necesario serializar la lista.
        }
    }
}

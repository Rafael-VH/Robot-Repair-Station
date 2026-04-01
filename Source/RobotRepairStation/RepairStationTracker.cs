using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RobotRepairStation
{
    /// <summary>
    /// Componente de mapa (<see cref="MapComponent"/>) que mantiene un registro
    /// centralizado de todas las <see cref="Building_RobotRepairStation"/> presentes
    /// en el mapa.
    /// <para>
    /// Propósito: los ThinkNodes (<see cref="ThinkNode_ConditionalNeedsRepair"/> y
    /// <see cref="JobGiver_GoToRepairStation"/>) necesitan encontrar estaciones
    /// disponibles cada vez que un mecanoid evalúa su IA. Sin este tracker, cada
    /// evaluación debería hacer una búsqueda costosa en todo el mapa. Con el tracker,
    /// la búsqueda es O(n) sobre una lista corta de estaciones ya conocidas.
    /// </para>
    /// <para>
    /// Ciclo de vida de las estaciones en el tracker:
    /// <list type="bullet">
    ///   <item><see cref="Building_RobotRepairStation.SpawnSetup"/> → <see cref="Register"/>.</item>
    ///   <item><see cref="Building_RobotRepairStation.DeSpawn"/> → <see cref="Deregister"/>.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class RepairStationTracker : MapComponent
    {
        // ─── Lista interna de estaciones ──────────────────────────────────────

        /// <summary>
        /// Lista de todas las estaciones registradas actualmente en el mapa.
        /// Se usa <see cref="List{T}"/> (no <see cref="HashSet{T}"/>) porque
        /// el orden importa para la búsqueda por distancia y el número de
        /// estaciones esperado es pequeño (&lt;20), por lo que el coste de
        /// búsqueda lineal es despreciable.
        /// </summary>
        private readonly List<Building_RobotRepairStation> stations =
            new List<Building_RobotRepairStation>();

        // ─── Propiedades públicas ─────────────────────────────────────────────

        /// <summary>
        /// Vista de solo lectura de todas las estaciones registradas en este mapa.
        /// <para>
        /// Usar <see cref="IReadOnlyList{T}"/> evita que código externo modifique
        /// la lista directamente; las mutaciones solo se permiten a través de
        /// <see cref="Register"/> y <see cref="Deregister"/>.
        /// </para>
        /// </summary>
        public IReadOnlyList<Building_RobotRepairStation> AllStations => stations;

        // ─── Constructor ──────────────────────────────────────────────────────

        /// <summary>
        /// Constructor requerido por el sistema de <see cref="MapComponent"/> de RimWorld.
        /// </summary>
        /// <param name="map">El mapa al que pertenece este componente.</param>
        public RepairStationTracker(Map map) : base(map) { }

        // ─── Fábrica estática ─────────────────────────────────────────────────

        /// <summary>
        /// Obtiene el <see cref="RepairStationTracker"/> existente del mapa,
        /// o crea uno nuevo y lo registra si no existe.
        /// <para>
        /// Es el punto de acceso preferido para obtener el tracker desde cualquier
        /// parte del código, ya que garantiza que el componente siempre existe
        /// antes de ser usado.
        /// </para>
        /// </summary>
        /// <param name="map">El mapa del que obtener/crear el tracker.</param>
        /// <returns>El <see cref="RepairStationTracker"/> del mapa especificado.</returns>
        public static RepairStationTracker GetOrCreate(Map map)
        {
            var existing = map.components.OfType<RepairStationTracker>().FirstOrDefault();
            if (existing != null) return existing;

            // Primera vez: crear e inyectar en la lista de componentes del mapa.
            var tracker = new RepairStationTracker(map);
            map.components.Add(tracker);
            return tracker;
        }

        // ─── Registro de estaciones ───────────────────────────────────────────

        /// <summary>
        /// Registra una estación en el tracker si aún no está en la lista.
        /// <para>
        /// Llamado desde <see cref="Building_RobotRepairStation.SpawnSetup"/>
        /// para asegurar que la estación sea visible para los ThinkNodes desde
        /// el momento en que aparece en el mapa.
        /// </para>
        /// </summary>
        /// <param name="station">La estación a registrar.</param>
        public void Register(Building_RobotRepairStation station)
        {
            if (!stations.Contains(station))
                stations.Add(station);
        }

        /// <summary>
        /// Elimina una estación del tracker.
        /// <para>
        /// Llamado desde <see cref="Building_RobotRepairStation.DeSpawn"/>
        /// para que los ThinkNodes no intenten navegar hacia una estación
        /// que ya no existe en el mapa.
        /// </para>
        /// </summary>
        /// <param name="station">La estación a desregistrar.</param>
        public void Deregister(Building_RobotRepairStation station)
        {
            stations.Remove(station);
        }

        // ─── Serialización ────────────────────────────────────────────────────

        /// <summary>
        /// Serializa el estado de este <see cref="MapComponent"/>.
        /// <para>
        /// La lista de estaciones en sí <b>no se serializa</b> aquí porque las
        /// propias estaciones se registran de nuevo en <see cref="Register"/>
        /// durante el <c>SpawnSetup</c> de cada edificio al cargar el mapa.
        /// Serializar la lista causaría referencias duplicadas.
        /// </para>
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // No se serializa `stations`: las estaciones se re-registran
            // automáticamente cuando el mapa carga sus buildings.
        }
    }
}

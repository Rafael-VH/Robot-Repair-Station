using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    /// <summary>
    /// Clase principal del edificio Robot Repair Station.
    /// <para>
    /// Extiende <c>Building</c> para añadir toda la lógica específica de la estación:
    /// registro en el <see cref="RepairStationTracker"/>, aceptación y expulsión de
    /// mecanoides, consumo de acero, persistencia de estado (save/load) y UI (gizmos,
    /// panel de inspección).
    /// </para>
    /// <para>
    /// Relación con otros componentes:
    /// <list type="bullet">
    ///   <item><see cref="CompRobotRepairStation"/> — tick de curación, delega aquí la gestión de ocupante.</item>
    ///   <item><see cref="JobDriver_GoToRepairStation"/> — llama a <see cref="TryAcceptOccupant"/>.</item>
    ///   <item><see cref="JobDriver_RepairAtStation"/> — observa <see cref="CurrentOccupant"/> en su tickAction.</item>
    ///   <item><see cref="RepairStationTracker"/> — registra/desregistra esta instancia para que los ThinkNodes puedan encontrarla.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class Building_RobotRepairStation : Building
    {
        // ─── Estado serializable ──────────────────────────────────────────────

        /// <summary>
        /// El mecanoid actualmente en proceso de reparación, o <c>null</c> si la
        /// estación está libre.
        /// <para>
        /// Se serializa como referencia (no copia) con <c>Scribe_References</c>
        /// para que después de cargar una partida apunte al mismo pawn, no a un
        /// duplicado.
        /// </para>
        /// </summary>
        private Pawn currentOccupant;

        /// <summary>
        /// Buffer interno de acero (unidades) listo para consumir sin tener que
        /// buscar en el mapa cada ciclo.
        /// <para>
        /// Se rellena en bloques de hasta <see cref="SteelBufferMax"/> unidades
        /// tomadas de pilas cercanas. Se serializa para que no se pierda entre
        /// sesiones de juego.
        /// </para>
        /// </summary>
        private int steelBuffer = 0;

        /// <summary>
        /// Capacidad máxima del buffer de acero interno.
        /// Limita cuántas unidades se cogen de una pila en un solo acceso.
        /// </summary>
        private const int SteelBufferMax = 50;

        // ─── Caché de comp properties ─────────────────────────────────────────

        /// <summary>
        /// Referencia cacheada a las <see cref="CompProperties_RobotRepairStation"/>
        /// para evitar llamar a <c>GetComp&lt;T&gt;()</c> — que hace una búsqueda
        /// lineal en la lista de comps — en cada tick.
        /// </summary>
        private CompProperties_RobotRepairStation cachedCompProps;

        // ─── Propiedades públicas ─────────────────────────────────────────────

        /// <summary>
        /// Acceso a las propiedades de configuración del comp, con lazy-init de caché.
        /// Devuelve <c>null</c> si el comp no está presente (no debería ocurrir en uso normal).
        /// </summary>
        public CompProperties_RobotRepairStation RepairProps =>
            cachedCompProps ?? (cachedCompProps = GetComp<CompRobotRepairStation>()?.Props);

        /// <summary>
        /// <c>true</c> si hay un mecanoid docked y ese mecanoid está vivo.
        /// Se usa como guardia en los ticks y en la lógica de decisión de los ThinkNodes.
        /// </summary>
        public bool IsOccupied  => currentOccupant != null && !currentOccupant.Dead;

        /// <summary>
        /// <c>true</c> si el <c>CompPowerTrader</c> asociado está alimentado y activo.
        /// Si no hay comp de energía (no debería darse), devuelve <c>false</c>.
        /// </summary>
        public bool HasPower    => this.TryGetComp<CompPowerTrader>()?.PowerOn ?? false;

        /// <summary>
        /// <c>true</c> si el buffer de acero tiene al menos 1 unidad disponible.
        /// Usado para la UI del panel de inspección.
        /// </summary>
        public bool HasSteel    => steelBuffer > 0;

        /// <summary>
        /// El mecanoid actualmente docked, o <c>null</c> si la estación está libre.
        /// Lectura pública; escritura solo a través de <see cref="TryAcceptOccupant"/>,
        /// <see cref="NotifyOccupantLeft"/> y <see cref="EjectOccupant"/>.
        /// </summary>
        public Pawn CurrentOccupant => currentOccupant;

        // ═══════════════════════════════════════════════════════════════════════
        //  CICLO DE VIDA DEL EDIFICIO
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Llamado por RimWorld cuando el edificio se coloca en el mapa
        /// (tanto en construcción nueva como al cargar una partida guardada).
        /// <para>
        /// Registra este edificio en el <see cref="RepairStationTracker"/> del mapa
        /// para que los ThinkNodes puedan encontrarlo sin hacer búsquedas caras
        /// en el mapa cada tick.
        /// </para>
        /// </summary>
        /// <param name="map">El mapa donde se está colocando el edificio.</param>
        /// <param name="respawningAfterLoad">
        /// <c>true</c> si el edificio se está restaurando desde una partida guardada.
        /// </param>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            RepairStationTracker.GetOrCreate(map).Register(this);
        }

        /// <summary>
        /// Llamado por RimWorld después de que todos los objetos del mapa han sido
        /// cargados y están listos (fase post-carga).
        /// <para>
        /// Valida que si había un ocupante guardado, ese ocupante aún tiene activo
        /// el job de reparación. Si no es así (p.ej. por un bug en una versión anterior
        /// o un conflicto con otro mod), limpia <c>currentOccupant</c> para desbloquear
        /// la estación en lugar de quedar en un estado inválido permanente.
        /// </para>
        /// </summary>
        public override void PostMapInit()
        {
            base.PostMapInit();

            if (currentOccupant == null) return;

            bool hasActiveJob =
                currentOccupant.CurJob?.def == RRS_JobDefOf.RRS_RepairAtStation ||
                currentOccupant.CurJob?.def == RRS_JobDefOf.RRS_GoToRepairStation;

            if (!hasActiveJob)
            {
                Log.Warning(
                    $"[RobotRepairStation] {currentOccupant.LabelShort} estaba" +
                    $" registrado en {Label} pero no tiene el job de reparación activo." +
                    " Limpiando estado para desbloquear la estación.");
                currentOccupant = null;
            }
        }

        /// <summary>
        /// Llamado cuando el edificio es eliminado del mapa (demolición, explosión, etc.).
        /// <para>
        /// Antes de llamar a la base:
        /// <list type="number">
        ///   <item>Desregistra la estación del <see cref="RepairStationTracker"/> del mapa.</item>
        ///   <item>
        ///     Expulsa al mecanoid si hay uno docked, para que no quede bloqueado
        ///     con un job que apunta a un edificio destruido.
        ///   </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="mode">Modo de destrucción (Vanish, Deconstruct, etc.).</param>
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            RepairStationTracker.GetOrCreate(Map).Deregister(this);
            EjectOccupant();
            base.DeSpawn(mode);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SERIALIZACIÓN (SAVE / LOAD)
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Serializa y deserializa el estado del edificio hacia/desde el archivo de partida.
        /// <para>
        /// Campos persistidos:
        /// <list type="bullet">
        ///   <item>
        ///     <c>currentOccupant</c>: como referencia (<c>Scribe_References</c>) para
        ///     que RimWorld resuelva el pawn correcto al cargar, evitando duplicados.
        ///   </item>
        ///   <item>
        ///     <c>steelBuffer</c>: como valor simple. El valor por defecto 0 garantiza
        ///     compatibilidad con partidas guardadas sin este campo.
        ///   </item>
        /// </list>
        /// </para>
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref currentOccupant, "currentOccupant");
            Scribe_Values.Look(ref steelBuffer, "steelBuffer", 0);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  TICK
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Llamado por RimWorld cada tick de juego (ticker type = Normal).
        /// <para>
        /// Responsabilidad del Tick del Building: gestión del consumo de acero
        /// (separada de la curación, que reside en <see cref="CompRobotRepairStation.CompTick"/>).
        /// </para>
        /// <para>
        /// Salidas tempranas para minimizar coste de CPU cuando no hay nada que hacer:
        /// sin energía → sin ocupante → sin props de comp → sin ciclo de consumo aún.
        /// </para>
        /// </summary>
        protected override void Tick()
        {
            base.Tick();

            if (!HasPower)           return;
            if (!IsOccupied)         return;
            if (RepairProps == null) return;

            // Consumir acero cada repairTickInterval ticks.
            if (Find.TickManager.TicksGame % RepairProps.repairTickInterval == 0)
                TryConsumeSteel();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  GESTIÓN DE OCUPANTE
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Intenta aceptar a un mecanoid como ocupante de la estación.
        /// <para>
        /// Llamado desde <see cref="JobDriver_GoToRepairStation"/> cuando el mecanoid
        /// llega a la celda de interacción de la estación.
        /// </para>
        /// </summary>
        /// <param name="mechanoid">El pawn mecanoid que solicita entrar.</param>
        /// <returns>
        /// <c>true</c> si el mecanoid fue aceptado; <c>false</c> si la estación
        /// ya tiene un ocupante o no tiene energía.
        /// </returns>
        public bool TryAcceptOccupant(Pawn mechanoid)
        {
            if (IsOccupied) return false;
            if (!HasPower)  return false;

            currentOccupant = mechanoid;
            return true;
        }

        /// <summary>
        /// Notifica al edificio que el ocupante ha completado su reparación
        /// y sale por su propia voluntad (job completado normalmente).
        /// <para>
        /// Solo limpia <c>currentOccupant</c>; NO termina el job ni libera
        /// reservas, porque el propio driver ya se encarga de eso al recibir
        /// la señal de que el ocupante es <c>null</c>.
        /// </para>
        /// <para>
        /// Llamado únicamente desde <see cref="CompRobotRepairStation.OnRepairComplete"/>.
        /// </para>
        /// </summary>
        public void NotifyOccupantLeft()
        {
            currentOccupant = null;
        }

        /// <summary>
        /// Fuerza la expulsión del mecanoid actualmente en la estación.
        /// <para>
        /// Escenarios que provocan una expulsión forzada:
        /// <list type="bullet">
        ///   <item>El buffer de acero se agota y no hay más en el mapa.</item>
        ///   <item>La estación pierde energía (gestionado externamente).</item>
        ///   <item>La estación es destruida (<see cref="DeSpawn"/>).</item>
        ///   <item>El jugador pulsa el gizmo de expulsión manual.</item>
        /// </list>
        /// </para>
        /// <para>
        /// Orden de operaciones para evitar race conditions:
        /// <list type="number">
        ///   <item>Captura el ocupante y limpia <c>currentOccupant</c> a <c>null</c> de inmediato.</item>
        ///   <item>Captura el job activo antes de terminarlo (EndCurrentJob puede cambiarlo).</item>
        ///   <item>Termina el job con <c>InterruptForced</c> si el pawn está en el job de reparación.</item>
        ///   <item>Libera la reserva usando el job capturado previamente.</item>
        /// </list>
        /// </para>
        /// </summary>
        public void EjectOccupant()
        {
            if (!IsOccupied) return;

            Pawn occupant   = currentOccupant;
            currentOccupant = null; // Limpiar antes de cualquier otra operación.

            // Capturar el job ANTES de llamar EndCurrentJob, ya que ese método
            // puede cambiar CurJob antes de retornar.
            Job jobToRelease = occupant.CurJob;

            if (occupant.CurJob?.def == RRS_JobDefOf.RRS_RepairAtStation)
                occupant.jobs.EndCurrentJob(JobCondition.InterruptForced);

            // Liberar la reserva del edificio (ocupa el slot 1/-1) para que
            // otros mecanoides puedan reservarlo en el futuro.
            if (jobToRelease != null)
                Map?.reservationManager.Release(this, occupant, jobToRelease);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  CONSUMO DE ACERO
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Intenta consumir acero del buffer interno para alimentar el ciclo de reparación.
        /// Si el buffer está vacío, busca acero en el mapa y lo añade al buffer.
        /// Si no hay acero disponible en ningún lado, expulsa al mecanoid y avisa al jugador.
        /// <para>
        /// Lógica de buffer:
        /// <list type="number">
        ///   <item>
        ///     Si <c>steelBuffer &gt;= steelPerRepairCycle</c>: descuenta y retorna.
        ///     Este es el camino "caliente" — no hace búsquedas en el mapa.
        ///   </item>
        ///   <item>
        ///     Si el buffer es insuficiente: busca la pila de acero más cercana
        ///     (radio 8 celdas) usando <c>GenClosest.ClosestThingReachable</c>.
        ///   </item>
        ///   <item>
        ///     Si se encuentra acero: toma hasta <see cref="SteelBufferMax"/> unidades
        ///     de la pila, recarga el buffer y descuenta el consumo del ciclo actual.
        ///   </item>
        ///   <item>
        ///     Si no hay acero: muestra un mensaje negativo y llama a
        ///     <see cref="EjectOccupant"/>.
        ///   </item>
        /// </list>
        /// </para>
        /// </summary>
        private void TryConsumeSteel()
        {
            int toConsume = RepairProps?.steelPerRepairCycle ?? 1;

            // Camino rápido: el buffer tiene suficiente acero para este ciclo.
            if (steelBuffer >= toConsume)
            {
                steelBuffer -= toConsume;
                return;
            }

            // Buffer insuficiente: buscar acero en el mapa.
            // Usar el pawn como referencia de TraverseParms para que respete
            // las reglas de acceso del mecanoid (puertas, regiones, etc.).
            TraverseParms traverseParams = currentOccupant != null
                ? TraverseParms.For(currentOccupant, Danger.Deadly)
                : TraverseParms.For(TraverseMode.NoPassClosedDoors);

            Thing steel = GenClosest.ClosestThingReachable(
                Position,
                Map,
                ThingRequest.ForDef(ThingDefOf.Steel),
                PathEndMode.ClosestTouch,
                traverseParams,
                maxDistance: 8f
            );

            if (steel != null)
            {
                // Tomar el mínimo entre la pila completa y la capacidad del buffer.
                int take = Mathf.Min(steel.stackCount, SteelBufferMax);

                // Reducir o destruir la pila de origen según las unidades tomadas.
                if (take >= steel.stackCount)
                    steel.Destroy(DestroyMode.Vanish);
                else
                    steel.stackCount -= take;

                // Recargar el buffer y descontar el consumo de este ciclo.
                // Mathf.Max(0, ...) evita valores negativos si toConsume > take (no debería ocurrir).
                steelBuffer = Mathf.Max(0, take - toConsume);
            }
            else
            {
                // Sin acero en el mapa: detener la reparación y notificar al jugador.
                Messages.Message(
                    "RRS_LetterNoSteelText".Translate(currentOccupant?.LabelShort ?? "mechanoid"),
                    this,
                    MessageTypeDefOf.NegativeEvent
                );
                EjectOccupant();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  UI — GIZMOS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Devuelve los gizmos (botones) que se muestran en la barra inferior de UI
        /// cuando el edificio está seleccionado.
        /// <para>
        /// Gizmos propios del mod:
        /// <list type="bullet">
        ///   <item>
        ///     <b>Eject mechanoid</b> (solo visible si hay un ocupante):
        ///     llama a <see cref="EjectOccupant"/> para forzar la salida.
        ///   </item>
        /// </list>
        /// Los gizmos base (encendido/apagado, etc.) se añaden antes mediante
        /// <c>base.GetGizmos()</c>.
        /// </para>
        /// </summary>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
                yield return g;

            // Botón de expulsión manual: solo disponible cuando hay un mecanoid docked.
            if (IsOccupied)
            {
                yield return new Command_Action
                {
                    defaultLabel = "RRS_GizmoEjectOccupant".Translate(),
                    defaultDesc  = "RRS_GizmoEjectOccupantDesc".Translate(),
                    icon         = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport"),
                    action       = EjectOccupant
                };
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  UI — PANEL DE INSPECCIÓN
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Devuelve el texto mostrado en el panel de inspección (parte inferior de la UI)
        /// cuando el edificio está seleccionado.
        /// <para>
        /// Información mostrada según el estado:
        /// <list type="bullet">
        ///   <item><b>Sin energía:</b> mensaje de offline.</item>
        ///   <item>
        ///     <b>Con ocupante:</b> nombre del mecanoid, porcentaje de salud actual,
        ///     y aviso si no hay acero en el buffer.
        ///   </item>
        ///   <item><b>Libre:</b> mensaje de espera.</item>
        ///   <item><b>Siempre:</b> nivel actual del buffer de acero.</item>
        /// </list>
        /// </para>
        /// </summary>
        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (!HasPower)
            {
                sb.AppendLine("RRS_InspectorNoPower".Translate());
            }
            else if (IsOccupied)
            {
                sb.AppendLine("RRS_InspectorCurrentOccupant".Translate(currentOccupant.LabelShort));
                sb.AppendLine($"Health: {currentOccupant.health.summaryHealth.SummaryHealthPercent * 100f:F0}%");
                if (!HasSteel)
                    sb.AppendLine("RRS_InspectorNoSteel".Translate());
            }
            else
            {
                sb.AppendLine("RRS_InspectorEmpty".Translate());
            }

            sb.Append($"Steel buffer: {steelBuffer}/{SteelBufferMax}");
            return sb.ToString().TrimEndNewlines();
        }
    }
}

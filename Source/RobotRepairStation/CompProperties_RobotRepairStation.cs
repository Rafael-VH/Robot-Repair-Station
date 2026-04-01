using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RobotRepairStation
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  COMP PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Propiedades configurables del componente de reparación.
    /// <para>
    /// Los valores se leen desde el XML del <c>ThingDef</c> del edificio
    /// (bloque <c>&lt;li Class="RobotRepairStation.CompProperties_RobotRepairStation"&gt;</c>
    /// dentro de <c>&lt;comps&gt;</c>) y son accesibles en runtime a través de
    /// <see cref="CompRobotRepairStation.Props"/>.
    /// </para>
    /// <para>
    /// Todos los campos tienen valores por defecto que se usan si la etiqueta
    /// correspondiente no está presente en el XML.
    /// </para>
    /// </summary>
    public class CompProperties_RobotRepairStation : CompProperties
    {
        // ─── Parámetros de reparación ─────────────────────────────────────────

        /// <summary>
        /// Fracción de salud (0-1) por debajo de la cual un mecanoid
        /// buscará activamente esta estación de reparación.
        /// <para>Valor por defecto: <c>0.5</c> (50 % de salud).</para>
        /// </summary>
        public float repairHealthThreshold = 0.5f;

        /// <summary>
        /// Cantidad de salud restaurada a cada lesión activa por tick de juego
        /// mientras el mecanoid está docked en la estación.
        /// <para>
        /// Valor por defecto: <c>0.0005</c>. Con el intervalo de consumo de
        /// acero (<see cref="repairTickInterval"/>) en 500 ticks y este valor,
        /// cada ciclo cura un total de ~0.25 HP por lesión.
        /// </para>
        /// </summary>
        public float repairSpeedPerTick = 0.0005f;

        /// <summary>
        /// Unidades de acero (<c>ThingDefOf.Steel</c>) consumidas cada vez que
        /// se ejecuta un ciclo de reparación (cada <see cref="repairTickInterval"/> ticks).
        /// <para>Valor por defecto: <c>1</c>.</para>
        /// </summary>
        public int steelPerRepairCycle = 1;

        /// <summary>
        /// Número de ticks de juego entre cada ciclo de consumo de acero.
        /// <para>
        /// Valor por defecto: <c>500</c> ticks (~8.3 segundos a velocidad ×1).
        /// Controla tanto la frecuencia de consumo de recursos como la granularidad
        /// de los mensajes de "sin acero".
        /// </para>
        /// </summary>
        public int repairTickInterval = 500;

        /// <summary>
        /// Distancia máxima en celdas a la que un mecanoid puede detectar y
        /// navegar hacia esta estación.
        /// <para>Valor por defecto: <c>30</c> celdas.</para>
        /// </summary>
        public float maxRepairRange = 30f;

        // ─── Constructor ──────────────────────────────────────────────────────

        /// <summary>
        /// Constructor requerido por el sistema de comps de RimWorld.
        /// <para>
        /// Vincula estas propiedades con su clase de comp correspondiente
        /// (<see cref="CompRobotRepairStation"/>), de forma que RimWorld puede
        /// instanciar el comp correcto al crear el edificio.
        /// </para>
        /// </summary>
        public CompProperties_RobotRepairStation()
        {
            compClass = typeof(CompRobotRepairStation);
        }
    }


    // ═══════════════════════════════════════════════════════════════════════════
    //  COMP — LÓGICA DE REPARACIÓN TICK A TICK
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Componente de lógica de reparación adjunto a <see cref="Building_RobotRepairStation"/>.
    /// <para>
    /// Responsabilidades de este comp:
    /// <list type="bullet">
    ///   <item>Ejecutar el ciclo de curación de lesiones activas cada tick.</item>
    ///   <item>Detectar cuándo el mecanoid ha alcanzado plena salud y notificarlo.</item>
    /// </list>
    /// El consumo de acero y la gestión del buffer residen en
    /// <see cref="Building_RobotRepairStation.TryConsumeSteel"/> para mantener
    /// separada la lógica de recursos de la lógica de curación.
    /// </para>
    /// </summary>
    public class CompRobotRepairStation : ThingComp
    {
        // ─── Acceso a propiedades y edificio ──────────────────────────────────

        /// <summary>
        /// Acceso tipado a las propiedades configurables del comp.
        /// </summary>
        public CompProperties_RobotRepairStation Props =>
            (CompProperties_RobotRepairStation)props;

        /// <summary>
        /// Referencia al edificio padre, casteada al tipo concreto para
        /// acceder a miembros propios de <see cref="Building_RobotRepairStation"/>.
        /// </summary>
        private Building_RobotRepairStation Station =>
            (Building_RobotRepairStation)parent;

        // ─── Tick ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Llamado por el engine de RimWorld cada tick de juego (60 ticks/segundo
        /// a velocidad normal).
        /// <para>
        /// Condiciones de salida temprana (sin coste de CPU):
        /// <list type="number">
        ///   <item>La estación no tiene energía.</item>
        ///   <item>No hay mecanoid docked.</item>
        ///   <item>El intervalo de ciclo aún no se ha cumplido.</item>
        /// </list>
        /// Solo cuando todas las condiciones se cumplen se llama a
        /// <see cref="ApplyRepairTick"/>, que es la operación costosa (LINQ sobre hediffs).
        /// </para>
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();

            if (!Station.HasPower)   return;
            if (!Station.IsOccupied) return;

            // Obtener el pawn docked; si está muerto (p.ej. por un evento externo)
            // no hacer nada — EjectOccupant se encargará en el siguiente tick del Building.
            Pawn pawn = Station.CurrentOccupant;
            if (pawn == null || pawn.Dead) return;

            // Ciclo de reparación: solo se ejecuta cada repairTickInterval ticks.
            if (Find.TickManager.TicksGame % Props.repairTickInterval == 0)
                ApplyRepairTick(pawn);
        }

        // ─── Lógica interna ───────────────────────────────────────────────────

        /// <summary>
        /// Aplica un tick de curación a todas las lesiones activas (no permanentes)
        /// del mecanoid actualmente docked.
        /// <para>
        /// Flujo:
        /// <list type="number">
        ///   <item>
        ///     Filtra los <see cref="Hediff_Injury"/> del pawn descartando los que
        ///     tienen el comp <c>HediffComp_GetsPermanent</c> con <c>IsPermanent = true</c>
        ///     (cicatrices, partes perdidas), ya que no se pueden curar con este sistema.
        ///   </item>
        ///   <item>
        ///     Llama a <c>Heal(repairSpeedPerTick)</c> sobre cada lesión activa.
        ///   </item>
        ///   <item>
        ///     Si la salud global supera el 99 %, llama a <see cref="OnRepairComplete"/>
        ///     para limpiar el estado y notificar al jugador.
        ///   </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="mechanoid">El pawn mecanoid actualmente docked en la estación.</param>
        private void ApplyRepairTick(Pawn mechanoid)
        {
            // Obtener todas las lesiones activas (no permanentes) del mecanoid.
            List<Hediff_Injury> injuries = mechanoid.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(h => !(h.TryGetComp<HediffComp_GetsPermanent>()?.IsPermanent ?? false))
                .ToList();

            // Curar cada lesión activa en la cantidad configurada por tick.
            foreach (Hediff_Injury injury in injuries)
                injury.Heal(Props.repairSpeedPerTick);

            // Verificar si la reparación está completa (≥99 % para evitar
            // que pequeñas imprecisiones de float impidan terminar).
            if (mechanoid.health.summaryHealth.SummaryHealthPercent >= 0.99f)
                OnRepairComplete(mechanoid);
        }

        /// <summary>
        /// Finaliza el proceso de reparación cuando el mecanoid ha recuperado
        /// toda su salud (≥99 %).
        /// <para>
        /// Acciones realizadas:
        /// <list type="number">
        ///   <item>Muestra un mensaje positivo al jugador con el nombre del mecanoid.</item>
        ///   <item>
        ///     Llama a <see cref="Building_RobotRepairStation.NotifyOccupantLeft"/>,
        ///     que pone <c>CurrentOccupant = null</c>. Esto hace que el
        ///     <c>tickAction</c> de <see cref="JobDriver_RepairAtStation"/> detecte
        ///     el cambio en el siguiente tick y llame a <c>EndJobWith(Succeeded)</c>,
        ///     terminando el job limpiamente.
        ///   </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="mechanoid">El mecanoid que ha terminado de repararse.</param>
        private void OnRepairComplete(Pawn mechanoid)
        {
            Messages.Message(
                "RRS_LetterRepairCompleteText".Translate(mechanoid.LabelShort),
                mechanoid,
                MessageTypeDefOf.PositiveEvent
            );

            // Limpiar el estado del edificio. El driver de reparación detectará
            // la ausencia del ocupante en su tickAction y terminará el job.
            Station.NotifyOccupantLeft();
        }
    }
}

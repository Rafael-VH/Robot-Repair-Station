using RimWorld;
using Verse;
using Verse.AI;

namespace RobotRepairStation
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  NODO CONDICIONAL DEL THINK TREE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nodo condicional del ThinkTree que determina si un mecanoid necesita ir
    /// a repararse.
    /// <para>
    /// Este nodo se inyecta al inicio del <c>ThinkNode_Priority</c> del ThinkTree
    /// <c>MechanoidConstant</c> mediante el patch XML en
    /// <c>Patches/MechanoidThinkTree.xml</c>, dándole alta prioridad frente a
    /// otros comportamientos de la IA.
    /// </para>
    /// <para>
    /// Árbol de evaluación de <see cref="Satisfied"/>:
    /// <list type="number">
    ///   <item>¿Es un mecanoid? → No: salir (false).</item>
    ///   <item>¿Es del jugador? → No: salir (false).</item>
    ///   <item>¿Ya está en un job de reparación (activo o en cola)? → Sí: salir (false).</item>
    ///   <item>¿Existe una estación disponible y alcanzable? → No: salir (false).</item>
    ///   <item>¿La salud es inferior al umbral configurado? → Sí: entrar al subárbol.</item>
    /// </list>
    /// Si se satisface, el subárbol contiene <see cref="JobGiver_GoToRepairStation"/>,
    /// que emite el job de navegación a la estación.
    /// </para>
    /// </summary>
    public class ThinkNode_ConditionalNeedsRepair : ThinkNode_Conditional
    {
        /// <summary>
        /// Evalúa si el pawn cumple todas las condiciones para buscar una estación de reparación.
        /// <para>
        /// NOTA DE RENDIMIENTO: este método se llama una vez por tick de IA por cada mecanoid.
        /// Las comprobaciones están ordenadas de más barata a más cara para que las salidas
        /// tempranas minimicen el trabajo en los casos comunes.
        /// </para>
        /// <para>
        /// <see cref="RepairStationUtility.FindBestRepairStationComp"/> es la operación más
        /// cara (itera el tracker + calcula distancias), por eso va al final.
        /// </para>
        /// </summary>
        /// <param name="pawn">El pawn cuya IA está siendo evaluada.</param>
        /// <returns>
        /// <c>true</c> si el pawn es un mecanoid del jugador con salud baja, no está ya
        /// en proceso de reparación, y hay una estación válida y accesible.
        /// </returns>
        protected override bool Satisfied(Pawn pawn)
        {
            // Verificaciones baratas primero.
            if (!pawn.RaceProps.IsMechanoid)      return false;
            if (pawn.Faction != Faction.OfPlayer)  return false;

            // Evitar interrumpir una reparación en curso.
            // Sin esta guard, un mecanoid con salud baja que ya está docked o
            // navegando hacia la estación recibiría un nuevo job RRS_GoToRepairStation
            // en cada ciclo de IA, interrumpiendo la reparación actual.
            if (pawn.CurJob?.def == RRS_JobDefOf.RRS_RepairAtStation   ||
                pawn.CurJob?.def == RRS_JobDefOf.RRS_GoToRepairStation)
                return false;

            // Búsqueda de estación (más cara): solo si pasó las verificaciones anteriores.
            var comp = RepairStationUtility.FindBestRepairStationComp(pawn);
            if (comp == null) return false;

            // Comparar la salud actual con el umbral configurado en el comp.
            return pawn.health.summaryHealth.SummaryHealthPercent < comp.Props.repairHealthThreshold;
        }
    }


    // ═══════════════════════════════════════════════════════════════════════════
    //  JOB GIVER — EMITE EL JOB DE NAVEGACIÓN
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nodo generador de jobs del ThinkTree. Emite el job
    /// <see cref="RRS_JobDefOf.RRS_GoToRepairStation"/> cuando hay una estación
    /// disponible para el mecanoid.
    /// <para>
    /// Este nodo es hijo directo de <see cref="ThinkNode_ConditionalNeedsRepair"/>
    /// en el ThinkTree, por lo que solo se evalúa si el condicional padre ya fue
    /// satisfecho. Aun así, repite la búsqueda de estación porque el ThinkTree
    /// puede saltar a este nodo de formas no estrictamente secuenciales y porque
    /// los ThinkNodes no tienen mecanismo para pasar datos entre sí.
    /// </para>
    /// </summary>
    public class JobGiver_GoToRepairStation : ThinkNode_JobGiver
    {
        /// <summary>
        /// Intenta generar un job de desplazamiento hacia la mejor estación de
        /// reparación disponible para el pawn.
        /// <para>
        /// Verificaciones adicionales respecto al condicional padre:
        /// <list type="bullet">
        ///   <item>
        ///     Comprueba que nadie más de la misma facción ya tiene reservada la estación,
        ///     excepto el propio pawn (por si ya tenía el job de una iteración anterior).
        ///   </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="pawn">El pawn para el que se genera el job.</param>
        /// <returns>
        /// Un <see cref="Job"/> de tipo <see cref="RRS_JobDefOf.RRS_GoToRepairStation"/>,
        /// o <c>null</c> si no hay estación disponible o ya está reservada.
        /// </returns>
        protected override Job TryGiveJob(Pawn pawn)
        {
            Building_RobotRepairStation station =
                RepairStationUtility.FindBestRepairStation(pawn);

            if (station == null) return null;

            // Si la estación ya está reservada por otro pawn de la misma facción,
            // no competir por ella; esperar a que quede libre.
            var reservationManager = pawn.Map?.reservationManager;
            if (reservationManager != null
                && reservationManager.IsReservedByAnyoneOf(station, pawn.Faction)
                && !reservationManager.ReservedBy(station, pawn))
            {
                return null;
            }

            return JobMaker.MakeJob(RRS_JobDefOf.RRS_GoToRepairStation, station);
        }
    }


    // ═══════════════════════════════════════════════════════════════════════════
    //  UTILIDADES DE BÚSQUEDA DE ESTACIONES
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Métodos de utilidad para encontrar la mejor estación de reparación
    /// disponible para un pawn mecanoid.
    /// <para>
    /// Centraliza la lógica de búsqueda para evitar duplicarla en
    /// <see cref="ThinkNode_ConditionalNeedsRepair"/> y
    /// <see cref="JobGiver_GoToRepairStation"/>.
    /// </para>
    /// <para>
    /// NOTA SOBRE LA DOBLE LLAMADA: <see cref="ThinkNode_ConditionalNeedsRepair.Satisfied"/>
    /// llama a <see cref="FindBestRepairStationComp"/> y, si retorna true, el ThinkTree
    /// evalúa <see cref="JobGiver_GoToRepairStation.TryGiveJob"/>, que llama a
    /// <see cref="FindBestRepairStation"/> de nuevo. Esta doble búsqueda es una
    /// limitación del sistema de ThinkNodes de RimWorld (los nodos no comparten estado).
    /// Con un número reducido de estaciones (típicamente &lt;10 por mapa) el coste es
    /// despreciable. Si en el futuro el número de estaciones creciera significativamente,
    /// considerar un caché por pawn+tick usando un diccionario estático limpiado al inicio
    /// de cada tick de IA.
    /// </para>
    /// </summary>
    public static class RepairStationUtility
    {
        /// <summary>
        /// Encuentra la estación de reparación más cercana que sea válida y
        /// alcanzable para el pawn especificado.
        /// <para>
        /// Criterios de validez de una estación:
        /// <list type="bullet">
        ///   <item>No es nula ni ha sido destruida.</item>
        ///   <item>Tiene energía activa.</item>
        ///   <item>No está ocupada por otro mecanoid (o está ocupada por el propio pawn).</item>
        ///   <item>El pawn puede llegar a ella (ruta no bloqueada, riesgo ≤ Deadly).</item>
        ///   <item>Está dentro del rango máximo configurado (<c>maxRepairRange</c>).</item>
        /// </list>
        /// Entre todas las válidas, se devuelve la de menor distancia euclídea.
        /// </para>
        /// </summary>
        /// <param name="pawn">El mecanoid que busca estación.</param>
        /// <returns>
        /// La <see cref="Building_RobotRepairStation"/> más cercana válida,
        /// o <c>null</c> si no hay ninguna disponible.
        /// </returns>
        public static Building_RobotRepairStation FindBestRepairStation(Pawn pawn)
        {
            if (pawn?.Map == null) return null;

            var tracker = RepairStationTracker.GetOrCreate(pawn.Map);
            Building_RobotRepairStation best = null;
            float bestDist = float.MaxValue;

            foreach (var station in tracker.AllStations)
            {
                if (station == null || station.Destroyed) continue;
                if (!station.HasPower)                    continue;
                if (station.IsOccupied && station.CurrentOccupant != pawn) continue;

                // CanReach hace una consulta al sistema de regiones del mapa;
                // es relativamente cara, por eso va después de los filtros baratos.
                if (!pawn.CanReach(station, PathEndMode.InteractionCell, Danger.Deadly)) continue;

                var comp = station.GetComp<CompRobotRepairStation>();
                float maxRange = comp?.Props.maxRepairRange ?? 30f;

                float dist = pawn.Position.DistanceTo(station.Position);
                if (dist > maxRange) continue;

                // Actualizar el mejor candidato si esta estación es más cercana.
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best     = station;
                }
            }

            return best;
        }

        /// <summary>
        /// Variante de <see cref="FindBestRepairStation"/> que devuelve directamente
        /// el <see cref="CompRobotRepairStation"/> de la mejor estación encontrada.
        /// <para>
        /// Útil cuando el llamador solo necesita acceder a las propiedades de
        /// configuración del comp (p.ej. el umbral de salud) sin necesitar la
        /// referencia al edificio.
        /// </para>
        /// </summary>
        /// <param name="pawn">El mecanoid que busca estación.</param>
        /// <returns>
        /// El <see cref="CompRobotRepairStation"/> de la mejor estación, o <c>null</c>.
        /// </returns>
        public static CompRobotRepairStation FindBestRepairStationComp(Pawn pawn)
        {
            var station = FindBestRepairStation(pawn);
            return station?.GetComp<CompRobotRepairStation>();
        }
    }
}

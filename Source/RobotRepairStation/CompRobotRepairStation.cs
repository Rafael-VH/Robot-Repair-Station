using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RobotRepairStation
{
    /// <summary>
    /// Propiedades XML del comp de la repair station.
    /// Todos los valores son configurables desde el ThingDef XML.
    /// </summary>
    public class CompProperties_RobotRepairStation : CompProperties
    {
        /// <summary>
        /// Fracción de salud (0–1) por debajo de la cual un mecanoid busca reparación.
        /// Default: 0.5 (50% de salud)
        /// </summary>
        public float repairHealthThreshold = 0.5f;

        /// <summary>
        /// Cuánta salud se restaura por tick de reparación mientras el mecanoid está docked.
        /// Default: 0.0005 por tick (~20% de salud por hora de juego a velocidad normal)
        /// </summary>
        public float repairSpeedPerTick = 0.0005f;

        /// <summary>
        /// Cantidad de acero consumido por ciclo de reparación.
        /// </summary>
        public int steelPerRepairCycle = 1;

        /// <summary>
        /// Ticks entre cada ciclo de reparación (consumo de acero + tick de salud).
        /// </summary>
        public int repairTickInterval = 500;

        /// <summary>
        /// Distancia máxima (en celdas) a la que un mecanoid detecta esta station.
        /// </summary>
        public float maxRepairRange = 30f;

        public CompProperties_RobotRepairStation()
        {
            compClass = typeof(CompRobotRepairStation);
        }
    }

    /// <summary>
    /// Comp adjunto al building de la repair station.
    /// Gestiona la lógica de reparación por tick aplicada al ocupante actual.
    ///
    /// FIX #1 (previo): ApplyRepairTick solo se ejecuta cada repairTickInterval ticks.
    /// FIX #2 (previo): OnRepairComplete es el único punto de eject autorizado.
    ///                  JobDriver_RepairAtStation detecta el eject indirectamente.
    /// FIX #7 (previo): hediff collection copiada a List antes de iterar para evitar
    ///                  InvalidOperationException cuando Heal() elimina un hediff.
    /// FIX I (nota de diseño): ApplyRepairTick solo cura Hediff_Injury.
    ///        Hediff_MissingPart (partes amputadas) NO se restaura — requeriría
    ///        pawn.health.RestorePart() y lógica adicional de regrow.
    ///        Un mecanoid con partes perdidas alcanzará el 99% de SummaryHealth
    ///        si sus heridas se curan, pero las partes amputadas permanecen.
    ///        Esta limitación está documentada en el README y en el XML description.
    ///        Para restaurar partes amputadas en una versión futura, iterar también
    ///        sobre hediffs.OfType&lt;Hediff_MissingPart&gt;() y llamar RestorePart().
    /// </summary>
    public class CompRobotRepairStation : ThingComp
    {
        public CompProperties_RobotRepairStation Props =>
            (CompProperties_RobotRepairStation)props;

        private Building_RobotRepairStation Station =>
            (Building_RobotRepairStation)parent;

        public override void CompTick()
        {
            base.CompTick();

            if (!Station.HasPower) return;
            if (!Station.IsOccupied) return;

            var pawn = Station.CurrentOccupant;
            if (pawn == null || pawn.Dead) return;

            // FIX #1: Solo curar en el intervalo configurado, no cada tick.
            if (Find.TickManager.TicksGame % Props.repairTickInterval == 0)
            {
                ApplyRepairTick(pawn);
            }
        }

        private void ApplyRepairTick(Pawn mechanoid)
        {
            // FIX #7: Copiar hediffs a una lista separada antes de iterar.
            // Heal() puede eliminar una herida completamente curada de la colección
            // en vivo, lo que lanzaría InvalidOperationException en medio del loop.
            List<Hediff_Injury> injuries = mechanoid.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(h => !h.IsOld())
                .ToList();

            foreach (Hediff_Injury injury in injuries)
            {
                injury.Heal(Props.repairSpeedPerTick);
            }

            // FIX #2: La verificación de completado ocurre aquí únicamente.
            // El job driver detecta el eject cuando CurrentOccupant pasa a null.
            if (mechanoid.health.summaryHealth.SummaryHealthPercent >= 0.99f)
            {
                OnRepairComplete(mechanoid);
            }
        }

        /// <summary>
        /// Único punto autorizado para declarar la reparación terminada.
        /// Envía mensaje positivo y expulsa al ocupante.
        /// El job driver detecta el eject via Station.CurrentOccupant != pawn.
        /// </summary>
        private void OnRepairComplete(Pawn mechanoid)
        {
            Messages.Message(
                "RRS_LetterRepairCompleteText".Translate(mechanoid.LabelShort),
                mechanoid,
                MessageTypeDefOf.PositiveEvent
            );

            Station.EjectOccupant();
        }
    }
}

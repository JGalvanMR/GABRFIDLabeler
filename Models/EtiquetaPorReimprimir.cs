namespace GABRFIDLabeler.Models
{
    /// <summary>Etiqueta propuesta para reimpresión, con su última actividad registrada.</summary>
    public class EtiquetaPorReimprimir
    {
        public string IdClaveInt { get; set; }

        /// <summary>Última actividad (catálogo o movimientos). Null = nunca ha tenido.</summary>
        public DateTime? UltimaActividad { get; set; }

        /// <summary>Días transcurridos desde la última actividad. Null = nunca ha tenido.</summary>
        public int? DiasSinMovimiento { get; set; }

        public string DiasTexto => DiasSinMovimiento.HasValue
            ? $"{DiasSinMovimiento.Value:N0} días"
            : "Sin lecturas";

        public string DetalleTexto => UltimaActividad.HasValue
            ? $"Última actividad: {UltimaActividad.Value:dd/MM/yyyy}"
            : "Nunca ha tenido movimiento";
    }
}

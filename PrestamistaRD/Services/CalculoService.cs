namespace PrestamistaRD.Services
{
    public static class CalculoService
    {
        // Interés mensual del periodo actual
        public static decimal CalcularInteresPeriodo(decimal capital, decimal porcentajeMensual)
            => Math.Round(capital * (porcentajeMensual / 100m), 2);

        // ¿Aplica mora? (si paga después del día objetivo)
        public static bool AplicaMora(DateTime diaPagoBase, DateTime fechaPago)
            => fechaPago.Date > diaPagoBase.Date;

        // Avanzar al siguiente periodo (YYYY-MM) y nueva "fecha base" de pago
        public static (string nuevoPeriodo, DateTime nuevoDiaPago) AvanzarPeriodo(string periodoActual, DateTime diaPagoBase)
        {
            var baseNext = diaPagoBase.AddMonths(1);
            var periodo = $"{baseNext:yyyy-MM}";
            return (periodo, baseNext);
        }
    }
}

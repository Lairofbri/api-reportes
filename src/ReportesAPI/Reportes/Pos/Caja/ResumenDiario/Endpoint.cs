using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Caja;

public static class ResumenDiarioEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/caja/resumen-diario", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? fecha,
            string? formato) =>
        {
            var dia = fecha ?? DateTime.Today;

            var sql = """
                SELECT c.id AS CajaId,
                       c.fecha_apertura AS FechaApertura,
                        c.monto_inicial AS MontoInicial,
                        c.monto_final AS MontoFinal,
                       c.estado,
                       u.nombre AS UsuarioNombre,
                       s.nombre AS SucursalNombre,
                       v.total_ventas AS TotalVentas,
                        v.cantidad_ordenes AS CantidadOrdenes,
                        v.ticket_promedio AS TicketPromedio
                FROM cajas c
                LEFT JOIN usuarios u ON u.id = c.usuario_apertura_id
                LEFT JOIN sucursales s ON s.id = c.sucursal_id
                LEFT JOIN LATERAL (
                    SELECT COUNT(DISTINCT o.id) AS CantidadOrdenes,
                           ROUND(SUM(p.total_pagado)::numeric, 2) AS TotalVentas,
                            ROUND(AVG(p.total_pagado)::numeric, 2) AS TicketPromedio
                    FROM ordenes o
                    JOIN pagos p ON p.orden_id = o.id
                    WHERE o.creado_en::date = c.fecha_apertura::date
                      AND o.estado = 'pagada'
                ) v ON true
                WHERE c.tenant_id = @tenantId
                  AND c.fecha_apertura::date = @dia
                ORDER BY C.fecha_apertura DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<ResumenCajaRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@dia", dia.Date)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfResumenDiario(resultados, dia.Date, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, fecha = dia.Date });
        });
    }

    private static async Task<IResult> PdfResumenDiario(List<ResumenCajaRow> data, DateTime fecha, PosDbContext db, Guid? tenantId)
    {
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Resumen Diario de Caja");
        pdf.Empresa(empresa)
           .Reporte("Resumen Diario de Caja")
           .Periodo($"{fecha:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.SucursalNombre ?? "Sin sucursal",
            r.Estado,
            r.MontoInicial.ToString("N2"),
            (r.MontoFinal ?? 0).ToString("N2"),
            (r.TotalVentas ?? 0).ToString("N2"),
            (r.CantidadOrdenes ?? 0).ToString("N0"),
            (r.TicketPromedio ?? 0).ToString("N2")
        });

        pdf.Tabla(
            headers: ["Sucursal", "Estado", "Inicial", "Final", "Ventas", "Ordenes", "Ticket Prom."],
            rows: rows
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"resumen-diario-caja-{fecha:yyyyMMdd}.pdf");
    }
}

public record ResumenCajaRow(
    Guid CajaId, DateTime FechaApertura,
    decimal MontoInicial, decimal? MontoFinal, string Estado,
    string? UsuarioNombre, string? SucursalNombre,
    decimal? TotalVentas, long? CantidadOrdenes, decimal? TicketPromedio);


using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Caja;

public static class CuadreCajaEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/caja/cuadre-caja", async (
            PosDbContext db,
            TenantContext tenantContext,
            Guid? cajaId,
            string? formato) =>
        {
            if (cajaId is null)
                return Results.BadRequest(new { error = "cajaId es requerido" });

            var sql = """
                SELECT c.id AS CajaId,
                       c.fecha_apertura AS FechaApertura,
                        c.monto_inicial AS MontoInicial,
                       c.monto_final AS MontoCerrado,
                       COALESCE(v.total_ventas, 0) AS VentasTotales,
                       COALESCE(m.total_ingresos, 0) AS IngresosManuales,
                       COALESCE(m.total_retiros, 0) AS RetirosManuales,
                       ROUND((c.monto_inicial + COALESCE(v.total_ventas, 0) + COALESCE(m.total_ingresos, 0) - COALESCE(m.total_retiros, 0))::numeric, 2) AS MontoEsperado,
                       CASE
                           WHEN c.monto_final IS NOT NULL
                           THEN ROUND((c.monto_final - (c.monto_inicial + COALESCE(v.total_ventas, 0) + COALESCE(m.total_ingresos, 0) - COALESCE(m.total_retiros, 0)))::numeric, 2)
                           ELSE NULL
                       END AS Diferencia
                FROM cajas c
                LEFT JOIN LATERAL (
                    SELECT ROUND(SUM(p.total_pagado)::numeric, 2) AS TotalVentas
                    FROM ordenes o
                    JOIN pagos p ON p.orden_id = o.id
                    WHERE o.creado_en >= c.fecha_apertura AND (o.creado_en <= c.fecha_cierre OR c.fecha_cierre IS NULL)
                      AND o.estado = 'pagada'
                ) v ON true
                LEFT JOIN LATERAL (
                    SELECT ROUND(SUM(mc.monto) FILTER (WHERE mc.tipo = 'ingreso')::numeric, 2) AS TotalIngresos,
                           ROUND(SUM(mc.monto) FILTER (WHERE mc.tipo = 'retiro')::numeric, 2) AS TotalRetiros
                    FROM movimientos_caja mc
                    WHERE mc.caja_id = c.id
                ) m ON true
                WHERE c.tenant_id = @tenantId
                  AND c.id = @cajaId
                """;

            var resultado = await db.Database.SqlQueryRaw<CuadreCajaRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@cajaId", cajaId.Value)
            ).FirstOrDefaultAsync();

            if (resultado is null)
                return Results.NotFound(new { error = "Caja no encontrada" });

            if (formato == "pdf")
                return await PdfCuadreCaja(resultado, cajaId.Value, db, tenantContext.TenantId);

            return Results.Ok(resultado);
        });
    }

    private static async Task<IResult> PdfCuadreCaja(CuadreCajaRow data, Guid cajaId, PosDbContext db, Guid? tenantId)
    {
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Cuadre de Caja");
        pdf.Empresa(empresa)
           .Reporte("Cuadre de Caja")
           .Periodo($"{data.FechaApertura:dd/MM/yyyy}")
           .Encabezado();

        var rows = new[]
        {
            new[] { "Monto Inicial", data.MontoInicial.ToString("N2") },
            new[] { "Ventas Totales", data.VentasTotales.ToString("N2") },
            new[] { "Ingresos Manuales", data.IngresosManuales.ToString("N2") },
            new[] { "Retiros Manuales", data.RetirosManuales.ToString("N2") },
            new[] { "Monto Esperado", data.MontoEsperado.ToString("N2") },
            new[] { "Monto Cierre", (data.MontoCerrado ?? 0).ToString("N2") },
            new[] { "Diferencia", (data.Diferencia ?? 0).ToString("N2") }
        };

        pdf.Tabla(
            headers: ["Concepto", "Monto"],
            rows: rows
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"cuadre-caja-{cajaId:N}.pdf");
    }
}

public record CuadreCajaRow(
    Guid CajaId, DateTime FechaApertura,
    decimal MontoInicial, decimal? MontoCerrado,
    decimal VentasTotales, decimal IngresosManuales, decimal RetirosManuales,
    decimal MontoEsperado, decimal? Diferencia);


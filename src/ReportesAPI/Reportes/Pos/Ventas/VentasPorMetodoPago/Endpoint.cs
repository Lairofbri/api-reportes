using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Pos.Ventas;

public static class VentasPorMetodoPagoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/pos/ventas/por-metodo-pago", async (
            PosDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today;
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT p.metodo,
                       c.label AS MetodoNombre,
                        COUNT(DISTINCT p.orden_id) AS TotalOrdenes,
                        SUM(p.total_pagado) AS TotalMonto
                FROM pagos p
                LEFT JOIN catalogos c ON c.grupo = 'metodos_pago' AND c.valor = p.metodo
                WHERE p.tenant_id = @tenantId
                  AND p.creado_en >= @de AND p.creado_en < @ha::date + 1
                GROUP BY p.metodo, c.label
                ORDER BY TotalMonto DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<VentasMetodoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfVentasPorMetodoPago(resultados, @de, @ha, db, tenantContext.TenantId);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfVentasPorMetodoPago(List<VentasMetodoRow> data, DateTime desde, DateTime hasta, PosDbContext db, Guid? tenantId)
    {
        var totalOrdenes = data.Sum(r => r.TotalOrdenes);
        var totalMonto = data.Sum(r => r.TotalMonto);
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);

        using var pdf = new PdfBuilder();
        pdf.Titulo("Ventas por Metodo de Pago");
        pdf.Empresa(empresa)
           .Reporte("Ventas por Metodo de Pago")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.MetodoNombre ?? r.Metodo,
            r.TotalOrdenes.ToString("N0"),
            r.TotalMonto.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Metodo de Pago", "Ordenes", "Monto"],
            rows: rows,
            totalRow: ["Total", totalOrdenes.ToString("N0"), totalMonto.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"ventas-por-metodo-pago-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record VentasMetodoRow(string Metodo, string? MetodoNombre, long TotalOrdenes, decimal TotalMonto);


using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class MontosFacturadosEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/montos-facturados", async (
            DteDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT DATE_TRUNC('month', d.fecha_emision) AS Periodo,
                       d.tipo_dte AS TipoDte,
                       COUNT(DISTINCT d.id) AS TotalDtes,
                       ROUND(SUM(d.total)::numeric, 2) AS TotalMonto,
                       ROUND(SUM(d.subtotal)::numeric, 2) AS TotalSubtotal,
                       ROUND(SUM(d.iva)::numeric, 2) AS TotalIva,
                       ROUND(SUM(d.total - COALESCE(d.iva, 0))::numeric, 2) AS TotalExento
                FROM dtes d
                WHERE d.tenant_id = @tenantId
                  AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                  AND d.estado = 'aceptado'
                GROUP BY DATE_TRUNC('month', d.fecha_emision), d.tipo_dte
                ORDER BY Periodo, d.tipo_dte
                """;

            var resultados = await db.Database.SqlQueryRaw<MontosFacturadosRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf")
                return await PdfMontosFacturados(db, tenantContext.TenantId, resultados, @de, @ha);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfMontosFacturados(DteDbContext db, Guid? tenantId, List<MontosFacturadosRow> data, DateTime desde, DateTime hasta)
    {
        var totalSubtotal = data.Sum(r => r.TotalSubtotal);
        var totalIva = data.Sum(r => r.TotalIva);
        var totalExento = data.Sum(r => r.TotalExento);
        var totalMonto = data.Sum(r => r.TotalMonto);
        var totalDtes = data.Sum(r => r.TotalDtes);

        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("Montos Facturados");
        pdf.Empresa(empresa)
           .Reporte("Montos Facturados")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.Periodo.ToString("MM/yyyy"),
            r.TipoDte,
            r.TotalSubtotal.ToString("N2"),
            r.TotalIva.ToString("N2"),
            r.TotalExento.ToString("N2"),
            r.TotalMonto.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Mes", "Tipo DTE", "Subtotal", "IVA", "Exento", "Total"],
            rows: rows,
            totalRow: ["Total", totalDtes.ToString("N0"), totalSubtotal.ToString("N2"), totalIva.ToString("N2"), totalExento.ToString("N2"), totalMonto.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"montos-facturados-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record MontosFacturadosRow(
    DateTime Periodo, string TipoDte,
    long TotalDtes, decimal TotalMonto,
    decimal TotalSubtotal, decimal TotalIva, decimal TotalExento);


using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class DtePorTipoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/por-tipo", async (
            DteDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT d.tipo_dte AS TipoDte,
                       COUNT(DISTINCT d.id) AS TotalDtes,
                       ROUND(SUM(d.total)::numeric, 2) AS TotalMonto,
                       ROUND(AVG(d.total)::numeric, 2) AS PromedioMonto,
                       ROUND(SUM(d.iva)::numeric, 2) AS TotalIva
                FROM dtes d
                WHERE d.tenant_id = @tenantId
                  AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                GROUP BY d.tipo_dte
                ORDER BY TotalDtes DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DteTipoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf") return await PdfDtePorTipo(db, tenantContext.TenantId, resultados, @de, @ha);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfDtePorTipo(DteDbContext db, Guid? tenantId, List<DteTipoRow> data, DateTime desde, DateTime hasta)
    {
        var totalDtes = data.Sum(r => r.TotalDtes);
        var totalMonto = data.Sum(r => r.TotalMonto);
        var totalIva = data.Sum(r => r.TotalIva);

        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("DTE por Tipo");
        pdf.Empresa(empresa)
           .Reporte("DTE por Tipo")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.TipoDte,
            r.TotalDtes.ToString("N0"),
            r.TotalMonto.ToString("N2"),
            r.TotalIva.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Tipo DTE", "Cantidad", "Monto", "IVA"],
            rows: rows,
            totalRow: ["Total", totalDtes.ToString("N0"), totalMonto.ToString("N2"), totalIva.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"dte-por-tipo-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record DteTipoRow(string TipoDte, long TotalDtes, decimal TotalMonto, decimal PromedioMonto, decimal TotalIva);

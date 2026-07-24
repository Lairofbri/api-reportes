using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Emisiones;

public static class DtePorEstadoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/emisiones/por-estado", async (
            DteDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT d.estado,
                       COUNT(DISTINCT d.id) AS TotalDtes,
                       ROUND(SUM(d.total)::numeric, 2) AS TotalMonto
                FROM dtes d
                WHERE d.tenant_id = @tenantId
                  AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                GROUP BY d.estado
                ORDER BY TotalDtes DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DteEstadoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf") return await PdfDtePorEstado(db, tenantContext.TenantId, resultados, @de, @ha);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfDtePorEstado(DteDbContext db, Guid? tenantId, List<DteEstadoRow> data, DateTime desde, DateTime hasta)
    {
        var totalDtes = data.Sum(r => r.TotalDtes);
        var totalMonto = data.Sum(r => r.TotalMonto);

        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("DTE por Estado");
        pdf.Empresa(empresa)
           .Reporte("DTE por Estado")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.Estado,
            r.TotalDtes.ToString("N0"),
            r.TotalMonto.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Estado", "DTEs", "Monto Total"],
            rows: rows,
            totalRow: ["Total", totalDtes.ToString("N0"), totalMonto.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"dte-por-estado-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record DteEstadoRow(string Estado, long TotalDtes, decimal TotalMonto);

using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Dte.Establecimientos;

public static class DtePorEstablecimientoEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/dte/establecimientos/por-establecimiento", async (
            DteDbContext db,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sql = """
                SELECT e.id AS EstablecimientoId,
                       E.nombre,
                       E.cod_estable_mh AS CodEstableMH,
                       E.cod_punto_venta_mh AS CodPuntoVentaMH,
                       COUNT(DISTINCT d.id) AS TotalDtes,
                       ROUND(SUM(d.total)::numeric, 2) AS TotalMonto
                FROM dtes d
                JOIN establecimientos e ON e.id = d.establecimiento_id
                WHERE d.tenant_id = @tenantId
                  AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                GROUP BY e.id, E.nombre, E.cod_estable_mh, E.cod_punto_venta_mh
                ORDER BY TotalDtes DESC
                """;

            var resultados = await db.Database.SqlQueryRaw<DteEstablecimientoRow>(
                sql,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf") return await PdfDtePorEstablecimiento(db, tenantContext.TenantId, resultados, @de, @ha);

            return Results.Ok(new { datos = resultados, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfDtePorEstablecimiento(DteDbContext db, Guid? tenantId, List<DteEstablecimientoRow> data, DateTime desde, DateTime hasta)
    {
        var totalDtes = data.Sum(r => r.TotalDtes);
        var totalMonto = data.Sum(r => r.TotalMonto);

        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("DTE por Establecimiento");
        pdf.Empresa(empresa)
           .Reporte("DTE por Establecimiento")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = data.Select(r => new[]
        {
            r.Nombre,
            r.TotalDtes.ToString("N0"),
            r.TotalMonto.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Establecimiento", "DTEs", "Monto Total"],
            rows: rows,
            totalRow: ["Total", totalDtes.ToString("N0"), totalMonto.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"dte-por-establecimiento-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record DteEstablecimientoRow(Guid EstablecimientoId, string Nombre, string CodEstableMH, string CodPuntoVentaMH, long TotalDtes, decimal TotalMonto);

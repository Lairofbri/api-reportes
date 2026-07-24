using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Consolidados;

public static class IngresosVsDteEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/consolidados/ingresos-vs-dte", async (
            PosDbContext posDb,
            DteDbContext dteDb,
            TenantContext tenantContext,
            DateTime? desde,
            DateTime? hasta,
            string? formato) =>
        {
            var @de = desde ?? DateTime.Today.AddMonths(-1);
            var @ha = hasta ?? DateTime.Today;

            var sqlPos = """
                SELECT DATE_TRUNC('month', o.creado_en) AS Mes,
                       ROUND(SUM(o.total)::numeric, 2) AS IngresosPos
                FROM ordenes o
                WHERE o.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                GROUP BY DATE_TRUNC('month', o.creado_en)
                ORDER BY Mes
                """;

            var ingresosPos = await posDb.Database.SqlQueryRaw<IngresosMesRow>(
                sqlPos,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            var sqlDte = """
                SELECT DATE_TRUNC('month', d.fecha_emision) AS Mes,
                       ROUND(SUM(d.total)::numeric, 2) AS IngresosPos
                FROM dtes d
                WHERE d.tenant_id = @tenantId
                  AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                  AND d.estado = 'aceptado'
                GROUP BY DATE_TRUNC('month', d.fecha_emision)
                ORDER BY Mes
                """;

            var dtesAceptados = await dteDb.Database.SqlQueryRaw<IngresosMesRow>(
                sqlDte,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            if (formato == "pdf") return await PdfIngresosVsDte(posDb, tenantContext.TenantId, ingresosPos, dtesAceptados, @de, @ha);

            var consolidado = ingresosPos
                .GroupJoin(dtesAceptados, p => p.Mes, d => d.Mes, (p, d) => new
                {
                    mes = p.Mes,
                    ingresos_pos = p.IngresosPos,
                    total_dte = d.FirstOrDefault()?.IngresosPos ?? 0,
                    diferencia = p.IngresosPos - (d.FirstOrDefault()?.IngresosPos ?? 0)
                })
                .OrderBy(r => r.mes)
                .ToList();

            return Results.Ok(new { datos = consolidado, desde = @de, hasta = @ha });
        });
    }

    private static async Task<IResult> PdfIngresosVsDte(PosDbContext db, Guid? tenantId, List<IngresosMesRow> ingresosPos, List<IngresosMesRow> dtesAceptados, DateTime desde, DateTime hasta)
    {
        var consolidado = ingresosPos
            .GroupJoin(dtesAceptados, p => p.Mes, d => d.Mes, (p, d) => new
            {
                Mes = p.Mes,
                IngresosPos = p.IngresosPos,
                TotalDte = d.FirstOrDefault()?.IngresosPos ?? 0,
                Diferencia = p.IngresosPos - (d.FirstOrDefault()?.IngresosPos ?? 0)
            })
            .OrderBy(r => r.Mes)
            .ToList();

        var totalPos = consolidado.Sum(r => r.IngresosPos);
        var totalDte = consolidado.Sum(r => r.TotalDte);
        var totalBrecha = consolidado.Sum(r => r.Diferencia);

        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("Ingresos vs DTE");
        pdf.Empresa(empresa)
           .Reporte("Ingresos vs DTE")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var rows = consolidado.Select(r => new[]
        {
            r.Mes.ToString("MM/yyyy"),
            r.IngresosPos.ToString("N2"),
            r.TotalDte.ToString("N2"),
            r.Diferencia.ToString("N2")
        });

        pdf.Tabla(
            headers: ["Periodo", "Ingresos POS", "Ingresos DTE", "Brecha"],
            rows: rows,
            totalRow: ["Total", totalPos.ToString("N2"), totalDte.ToString("N2"), totalBrecha.ToString("N2")]
        );

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"ingresos-vs-dte-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record IngresosMesRow(DateTime Mes, decimal IngresosPos);

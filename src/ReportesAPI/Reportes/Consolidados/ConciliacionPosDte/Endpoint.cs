using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;
using ReportesAPI.Compartido.Pdf;
using ReportesAPI.Datos;

namespace ReportesAPI.Reportes.Consolidados;

public static class ConciliacionPosDteEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/reportes/consolidados/conciliacion-pos-dte", async (
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
                SELECT o.id AS OrdenId,
                       o.numero_orden AS NumeroOrden,
                       o.total,
                       o.creado_en AS CreadoEn,
                       o.sucursal_id AS SucursalId
                FROM ordenes o
                WHERE o.tenant_id = @tenantId
                  AND o.creado_en >= @de AND o.creado_en < @ha::date + 1
                  AND o.estado = 'pagada'
                ORDER BY O.creado_en
                """;

            var ordenesPagadas = await posDb.Database.SqlQueryRaw<OrdenParaConciliar>(
                sqlPos,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            var sqlDte = """
                SELECT d.id AS DteId,
                       d.codigo_generacion AS CodigoGeneracion,
                       d.tipo_dte AS TipoDte,
                       d.fecha_emision AS FechaEmision,
                       d.total,
                       d.orden_id AS OrdenId
                FROM dtes d
                WHERE d.tenant_id = @tenantId
                  AND d.fecha_emision >= @de AND d.fecha_emision < @ha::date + 1
                  AND d.orden_id IS NOT NULL
                """;

            var dtesEmitidos = await dteDb.Database.SqlQueryRaw<DteParaConciliar>(
                sqlDte,
                new NpgsqlParameter("@tenantId", tenantContext.TenantId!),
                new NpgsqlParameter("@de", @de),
                new NpgsqlParameter("@ha", @ha)
            ).ToListAsync();

            var ordenesConDte = dtesEmitidos.Select(d => d.OrdenId).ToHashSet();
            var ordenesSinDte = ordenesPagadas
                .Where(o => !ordenesConDte.Contains(o.OrdenId))
                .ToList();

            if (formato == "pdf")
                return await PdfConciliacionPosDte(posDb, tenantContext.TenantId, ordenesPagadas, dtesEmitidos, ordenesSinDte, @de, @ha);

            return Results.Ok(new
            {
                periodo = new { desde = @de, hasta = @ha },
                resumen = new
                {
                    total_ordenes_pagadas = ordenesPagadas.Count,
                    total_dtes_emitidos = dtesEmitidos.Count,
                    ordenes_sin_dte = ordenesSinDte.Count,
                    porcentaje_conciliado = ordenesPagadas.Count > 0
                        ? Math.Round((double)(ordenesPagadas.Count - ordenesSinDte.Count) / ordenesPagadas.Count * 100, 2)
                        : 0
                },
                ordenes_sin_dte = ordenesSinDte.Select(o => new
                {
                    o.OrdenId, o.NumeroOrden, o.Total, o.CreadoEn, o.SucursalId
                })
            });
        });
    }

    private static async Task<IResult> PdfConciliacionPosDte(PosDbContext db, Guid? tenantId, List<OrdenParaConciliar> ordenesPagadas, List<DteParaConciliar> dtesEmitidos, List<OrdenParaConciliar> ordenesSinDte, DateTime desde, DateTime hasta)
    {
        using var pdf = new PdfBuilder();
        var empresa = await PdfHelper.GetTenantNombreAsync(db, tenantId);
        pdf.Titulo("Conciliacion POS vs DTE");
        pdf.Empresa(empresa)
           .Reporte("Conciliacion POS vs DTE")
           .Periodo($"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}")
           .Encabezado();

        var conciliados = ordenesPagadas.Count - ordenesSinDte.Count;
        var porcentaje = ordenesPagadas.Count > 0
            ? Math.Round((double)conciliados / ordenesPagadas.Count * 100, 2)
            : 0;

        var resumenRows = new[]
        {
            new[] { "Total Ordenes Pagadas", ordenesPagadas.Count.ToString("N0") },
            new[] { "Total DTEs Emitidos", dtesEmitidos.Count.ToString("N0") },
            new[] { "Ordenes sin DTE", ordenesSinDte.Count.ToString("N0") },
            new[] { "Porcentaje Conciliado", porcentaje.ToString("N2") + "%" }
        };

        pdf.Tabla(
            headers: ["Indicador", "Cantidad"],
            rows: resumenRows
        );

        if (ordenesSinDte.Count > 0)
        {
            var detalleRows = ordenesSinDte.Select(o => new[]
            {
                o.NumeroOrden.ToString(),
                o.Total.ToString("N2"),
                o.CreadoEn.ToString("dd/MM/yyyy HH:mm"),
                o.SucursalId?.ToString() ?? ""
            });

            pdf.Tabla(
                headers: ["# Orden", "Total", "Fecha", "Sucursal"],
                rows: detalleRows
            );
        }

        pdf.PiePagina($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
        return Results.File(pdf.Generar(), "application/pdf", $"conciliacion-pos-dte-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.pdf");
    }
}

public record OrdenParaConciliar(Guid OrdenId, long NumeroOrden, decimal Total, DateTime CreadoEn, Guid? SucursalId);
public record DteParaConciliar(Guid DteId, string CodigoGeneracion, string TipoDte, DateTime FechaEmision, decimal Total, Guid? OrdenId);

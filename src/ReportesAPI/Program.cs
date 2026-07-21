using Microsoft.EntityFrameworkCore;
using ReportesAPI.Datos;
using ReportesAPI.Reportes.Pos.Ventas;
using ReportesAPI.Reportes.Pos.Productos;
using ReportesAPI.Reportes.Pos.Ordenes;
using ReportesAPI.Reportes.Pos.Propinas;
using ReportesAPI.Reportes.Pos.Caja;
using ReportesAPI.Reportes.Pos.Cocina;
using ReportesAPI.Reportes.Pos.Sucursales;
using ReportesAPI.Reportes.Pos.Staff;
using ReportesAPI.Reportes.Dte.Emisiones;
using ReportesAPI.Reportes.Dte.Establecimientos;
using ReportesAPI.Reportes.Dte.Anulaciones;
using ReportesAPI.Reportes.Dte.Contingencia;
using ReportesAPI.Reportes.Dte.Auditoria;
using ReportesAPI.Reportes.Consolidados;

var builder = WebApplication.CreateBuilder(args);

var connPos = builder.Configuration.GetConnectionString("PosDb")!;
var connDte = builder.Configuration.GetConnectionString("DteDb")!;
var connReports = builder.Configuration.GetConnectionString("ReportsDb")!;

builder.Services.AddDbContext<PosDbContext>(o =>
    o.UseNpgsql(connPos).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

builder.Services.AddDbContext<DteDbContext>(o =>
    o.UseNpgsql(connDte).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

builder.Services.AddDbContext<ReportesDbContext>(o =>
    o.UseNpgsql(connReports));

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();
app.UseMiddleware<ReportesAPI.Compartido.Auth.ApiKeyMiddleware>();

app.MapGet("/health", () => Results.Ok(new { estado = "ok", timestamp = DateTime.UtcNow }));

// POS
VentasPorPeriodoEndpoint.Map(app);
VentasPorMetodoPagoEndpoint.Map(app);
VentasPorSucursalEndpoint.Map(app);
VentasPorTipoOrdenEndpoint.Map(app);
VentasPorOrigenEndpoint.Map(app);
TopProductosVendidosEndpoint.Map(app);
IngresosPorCategoriaEndpoint.Map(app);
StockBajoEndpoint.Map(app);
TicketPromedioEndpoint.Map(app);
HorasPicoEndpoint.Map(app);
OrdenesCanceladasEndpoint.Map(app);
PropinasPorMeseroEndpoint.Map(app);
ResumenDiarioEndpoint.Map(app);
CuadreCajaEndpoint.Map(app);
TiempoPreparacionEndpoint.Map(app);
ComparativaSucursalesEndpoint.Map(app);
DesempenoMeseroEndpoint.Map(app);

// DTE
DtePorPeriodoEndpoint.Map(app);
DtePorTipoEndpoint.Map(app);
DtePorEstadoEndpoint.Map(app);
DteRechazadosEndpoint.Map(app);
MontosFacturadosEndpoint.Map(app);
TasaAceptacionEndpoint.Map(app);
DtePorEstablecimientoEndpoint.Map(app);
DteAnuladosEndpoint.Map(app);
EventosContingenciaEndpoint.Map(app);
PendientesResolucionEndpoint.Map(app);
TasaResolucionEndpoint.Map(app);
LogOperacionesEndpoint.Map(app);

// Consolidados
ConciliacionPosDteEndpoint.Map(app);
IngresosVsDteEndpoint.Map(app);

app.Run();

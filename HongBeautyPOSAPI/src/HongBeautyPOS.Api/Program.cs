using HongBeautyPOS.Api.Configuration;
using HongBeautyPOS.Api.Endpoints;
using HongBeautyPOS.Api.Startup;

var builder = WebApplication.CreateBuilder(args);
var settings = AppSettings.Load(builder.Configuration);
builder.Services.AddPosServices(settings);

var app = builder.Build();
app.UsePosPipeline(settings);
app.MapHealth();

if (await StartupTasks.TryRunCommandAsync(app, args)) return;
await StartupTasks.RunOnStartAsync(app, settings);

var api = app.MapGroup("/api/v1");
api.MapAuthEndpoints();
api.MapGroup(string.Empty).RequireAuthorization()
    .MapMeEndpoints()
    .MapCatalogEndpoints()
    .MapCustomerEndpoints()
    .MapSupplierEndpoints()
    .MapInventoryEndpoints()
    .MapPurchaseEndpoints()
    .MapSalesEndpoints()
    .MapCashEndpoints()
    .MapReportEndpoints()
    .MapEmployeeEndpoints()
    .MapAuditEndpoints();

app.MapSpaFallback();
app.Run();

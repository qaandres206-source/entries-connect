using cwApp.Components;
using cwApp.Models;
using cwApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Register ConnectWise services
builder.Services.AddHttpClient();
builder.Services.AddScoped<ConnectWiseService>();
builder.Services.AddScoped<ConfigStorageService>();

// ITGlue bridge (Fase 3). La API key se inyecta server-side vía variables de entorno
// (ITGLUE_API_KEY, ITGLUE_BASE_URL, ITGLUE_WEB_URL) — nunca en el navegador.
builder.Services.AddSingleton(new ItGlueOptions
{
    ApiKey = builder.Configuration["ITGLUE_API_KEY"] ?? "",
    BaseUrl = builder.Configuration["ITGLUE_BASE_URL"] ?? "https://api.itglue.com",
    WebUrl = builder.Configuration["ITGLUE_WEB_URL"]
});
builder.Services.AddScoped<ItGlueService>();
builder.Services.AddScoped<TicketContextService>();

// Reportes .xlsx (descarga en el navegador, sin envío por email).
builder.Services.AddScoped<ReportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(cwApp.Client._Imports).Assembly);

app.Run();

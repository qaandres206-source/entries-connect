using cwApp.Components;
using cwApp.Models;
using cwApp.Services;
using Microsoft.AspNetCore.HttpOverrides;

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

// Proxy de las imágenes incrustadas en las notas. Singleton porque el navegador
// pide /note-image/{token} en peticiones aparte del circuito de Blazor.
builder.Services.AddSingleton<NoteImageProxy>();

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

// La app corre detrás del proxy de Render (que ya termina TLS y fuerza HTTPS).
// Estas cabeceras le permiten conocer el esquema y la IP real del cliente; por eso
// no se usa UseHttpsRedirection() aquí (provocaba el warning "Failed to determine
// the https port for redirect").
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseAntiforgery();

app.MapStaticAssets();

// Sirve una imagen incrustada en una nota de ConnectWise. El token solo lo puede haber
// emitido esta app (va firmado con un secreto del proceso) y apunta a una URL del propio
// ConnectWise del usuario; ver NoteImageProxy para el porqué de esa restricción.
app.MapGet("/note-image/{token}", async (
    string token, NoteImageProxy proxy, IHttpClientFactory factory,
    HttpResponse response, CancellationToken ct) =>
{
    var image = await proxy.FetchAsync(token, factory, ct);
    if (image is null) return Results.NotFound();

    // Privada y de corta vida: la imagen es de un ticket, no debe cachearla ningún proxy
    // intermedio, pero sí el navegador para no volver a pedirla al hacer scroll.
    response.Headers.CacheControl = "private, max-age=3600";
    return Results.File(image.Value.Bytes, image.Value.ContentType);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(cwApp.Client._Imports).Assembly);

app.Run();

using QuestPDF.Infrastructure;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Puerto fijo local para instalación
builder.WebHost.UseUrls("http://localhost:5050");

// 🔹 Servicios MVC
builder.Services.AddControllersWithViews();

// 🔹 Inyección de dependencias
builder.Services.AddSingleton<PrestamistaRD.Data.Db>();

// 🔹 Sesiones
builder.Services.AddSession();

// 🔹 HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// 🔹 Licencia de QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// 🔹 Manejo de errores en producción
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Cuenta}/{action=Login}/{id?}")
    .WithStaticAssets();

// ⚠️ Instalador: No abrir el navegador aquí.
// El archivo .BAT lo hará.

app.Run();

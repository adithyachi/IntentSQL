using BizPulse.AI.POC.Data;
using Microsoft.EntityFrameworkCore;
using BizPulse.AI.POC.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMemoryCache();


// ================================================================
// LOCAL QWEN - OLLAMA
// ================================================================

builder.Services.AddHttpClient<OllamaService>(client =>
{
    client.BaseAddress =
        new Uri("http://localhost:11434/");

    client.Timeout =
        TimeSpan.FromMinutes(30);
});


// ================================================================
// CLOUD QWEN - TOGETHER AI
// ================================================================

builder.Services.AddHttpClient<TogetherAiService>(client =>
{
    client.BaseAddress =
        new Uri("https://api.together.ai/");

    client.Timeout =
        TimeSpan.FromMinutes(30);
});


// ================================================================
// APPLICATION SERVICES
// ================================================================

builder.Services.AddScoped<DatabaseSchemaService>();

builder.Services.AddScoped<SqlGenerationService>();

builder.Services.AddScoped<SqlExecutionService>();

builder.Services.AddScoped<DataExplorerService>();


var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
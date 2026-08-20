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

// ================================================================
// INTENTSQL KILL SWITCH
// ================================================================
//
// Environment variable:
//     INTENTSQL_ENABLED=true   -> application enabled
//     INTENTSQL_ENABLED=false  -> application disabled
//
// If the variable does not exist, the application remains enabled.
//

var intentSqlEnabled =
    builder.Configuration.GetValue<bool>("IntentSQL:Enabled");

// ================================================================
// CONFIGURE HTTP REQUEST PIPELINE
// ================================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();


// ================================================================
// INTENTSQL KILL SWITCH MIDDLEWARE
// ================================================================

app.Use(async (context, next) =>
{
    if (!intentSqlEnabled)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        context.Response.ContentType = "text/html; charset=utf-8";

        await context.Response.WriteAsync("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />

                <title>IntentSQL</title>

                <style>
                    body {
                        margin: 0;
                        min-height: 100vh;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        background: #f8f9fa;
                        font-family: Arial, Helvetica, sans-serif;
                        color: #212529;
                    }

                    .message {
                        text-align: center;
                        padding: 40px;
                    }

                    h1 {
                        font-size: 2rem;
                        margin-bottom: 15px;
                    }

                    p {
                        font-size: 1.1rem;
                        color: #6c757d;
                    }
                </style>
            </head>

            <body>

                <div class="message">

                    <h1>
                        ☕ IntentSQL is taking a coffee break.
                    </h1>

                    <p>
                        Even AI needs caffeine. ☕🤖
                    </p>

                </div>

            </body>
            </html>
            """);

        return;
    }

    await next();
});

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
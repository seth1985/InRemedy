using InRemedy.Api.Data;
using InRemedy.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    Environment.GetEnvironmentVariable("INREMEDY_CONNECTION_STRING") ??
    builder.Configuration.GetConnectionString("InRemedy") ??
    "Host=localhost;Port=5432;Database=inremedy;Username=postgres;Password=postgres";

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddDbContext<InRemedyDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024L * 1024L * 1024L;
});
builder.Services.AddSingleton<ImportQueue>();
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddHostedService<ImportWorker>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await PostgresBootstrapper.EnsureDatabaseExistsAsync(connectionString, app.Lifetime.ApplicationStopping);

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<InRemedyDbContext>();
    var latestMigration = dbContext.Database.GetMigrations().LastOrDefault();
    if (!string.IsNullOrWhiteSpace(latestMigration))
    {
        await PostgresBootstrapper.EnsureMigrationHistoryBaselineAsync(
            connectionString,
            latestMigration,
            "8.0.5",
            app.Lifetime.ApplicationStopping);
    }

    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("frontend");
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();

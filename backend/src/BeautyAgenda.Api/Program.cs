using System.Text.Json.Serialization;
using BeautyAgenda.Api.Infrastructure;
using BeautyAgenda.Application;
using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Infrastructure;
using BeautyAgenda.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddApiAuthentication();
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddApiDocumentation();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "BeautyAgenda API v1");
        o.RoutePrefix = "swagger";
    });
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

app.Run();

public partial class Program;

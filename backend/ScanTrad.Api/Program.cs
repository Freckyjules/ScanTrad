using Microsoft.EntityFrameworkCore;
using ScanTrad.Application.Interfaces;
using ScanTrad.Application.Services;
using ScanTrad.Domain.Repositories;
using ScanTrad.Infrastructure.Data;
using ScanTrad.Infrastructure.Repositories;
using ScanTrad.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<ScanTradDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ScanTrad")));

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Crée la base au premier lancement et applique les migrations en attente.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ScanTradDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

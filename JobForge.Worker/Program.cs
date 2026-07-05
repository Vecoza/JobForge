using JobForge.Core;
using JobForge.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.Configure<MailtrapOptions>(builder.Configuration.GetSection("Mailtrap"));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

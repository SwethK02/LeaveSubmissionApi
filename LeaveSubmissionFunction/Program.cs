using LeaveSubmissionFunction.Data;
using LeaveSubmissionFunction.Services;
using LeaveSubmissionFunction.Validators;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // Register connection string from app settings
        var connectionString = context.Configuration["SqlConnectionString"]
            ?? throw new InvalidOperationException("SqlConnectionString is not configured.");

        services.AddSingleton<ILeaveRepository>(sp =>
            new LeaveRepository(connectionString));

        services.AddScoped<ILeaveSubmissionService, LeaveSubmissionService>();
        services.AddScoped<LeaveSubmissionValidator>();
    })
    .Build();

host.Run();

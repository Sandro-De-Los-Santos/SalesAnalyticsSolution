using ETL.App;
using ETL.Core.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("ExternalApi", client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"];
    client.BaseAddress = new Uri(baseUrl!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton(sp =>
{
    var path = builder.Configuration["StagingSettings:OutputPath"] ?? "Staging";
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("StagingWriter");
    return new StagingWriter(path, logger);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
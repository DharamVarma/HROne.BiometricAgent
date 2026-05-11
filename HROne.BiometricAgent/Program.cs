using HROne.BiometricAgent;
using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using HROne.BiometricAgentService.Biz;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
var configManager = builder.Configuration;
var section = builder.Configuration.GetSection("AppSettings");
builder.Services.Configure<AppSettings>(section);
builder.Services.AddSingleton(resolver => resolver.GetRequiredService<IOptions<AppSettings>>().Value);
builder.Logging.AddLog4Net(ConstLogger.LogConfigFile);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAppLoggerFactory, Log4NetLoggerFactory>();
builder.Services.AddScoped<IBiometricDataService, BiometricDataService>();
builder.Services.AddScoped<IPushBiometricDbFactory, PushBiometricDbFactory>();
builder.Services.AddScoped<IBiometricErrorLogService, BiometricErrorLogService>();
builder.Services.AddScoped<IBiometricMailService, BiometricMailService>();
builder.Services.AddScoped<ITransferBiometricBulkDataService, TransferBiometricBulkDataService>();
builder.Services.AddScoped<PushBiometricSqlDbService>();
builder.Services.AddScoped<PushBiometricMySqlDbService>();
builder.Services.AddScoped<PushBiometricPostgreSqlDbService>();
#pragma warning disable CA1416 // Validate platform compatibility
builder.Services.AddScoped<PushBiometricMsAccessDbService>();
#pragma warning restore CA1416 // Validate platform compatibility

builder.Services.AddHostedService<Worker>();

var appSettings = section.Get<AppSettings>();
if (!appSettings.AllowWriteFullLog)
    builder.Logging.ClearProviders();
// Access the Version property from appSettings
var appVersion = appSettings.Version;
// set the log4net logger by include the version
log4net.GlobalContext.Properties["AppVersion"] = appVersion;


var host = builder.Build();
await host.RunAsync();


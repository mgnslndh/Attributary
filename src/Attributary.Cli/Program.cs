using Attributary.Cli;
using Attributary.Cli.Commands;
using Attributary.Cli.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddSingleton(AnsiConsole.Console);
services.AddSingleton<GenerateRunner>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);
app.Configure(config =>
{
    config.AddCommand<GenerateCommand>("generate");
    config.AddBranch("license", license =>
    {
        license.AddCommand<LicenseListCommand>("list");
        license.AddCommand<LicenseShowCommand>("show");
    });
    config.AddBranch("cache", cache =>
    {
        cache.AddCommand<CacheClearCommand>("clear");
        cache.AddCommand<CacheListCommand>("list");
        cache.AddCommand<CachePathCommand>("path");
    });
    config.AddCommand<InitCommand>("init");
});

return app.Run(args);

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
});

return app.Run(args);

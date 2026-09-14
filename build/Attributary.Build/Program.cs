using Attributary.Build;
using Cake.Frosting;

return new CakeHost()
    .UseContext<BuildContext>()
    .UseLifetime<BuildLifetime>()
    .Run(args);

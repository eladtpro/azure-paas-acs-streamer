[assembly: FunctionsStartup(typeof(RadioArchive.Startup))]

namespace RadioArchive;
public class Startup : FunctionsStartup
{


    // public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
    // {
    //     FunctionsHostBuilderContext context = builder.GetContext();

    //     var config = builder.ConfigurationBuilder
    //         .SetBasePath(context.ApplicationRootPath)
    //         .AddJsonFile(Path.Combine(context.ApplicationRootPath, "local.settings.json"), optional: true, reloadOnChange: true)
    //         .AddJsonFile(Path.Combine(context.ApplicationRootPath, "appsettings.json"), optional: true, reloadOnChange: false)
    //         .AddJsonFile(Path.Combine(context.ApplicationRootPath, $"appsettings.{context.EnvironmentName}.json"), optional: true, reloadOnChange: false)
    //         .AddEnvironmentVariables()
    //         .Build();
    // }

    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddOptions<Settings>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection(Settings.Section).Bind(settings);
            });


        // builder.Services.AddOptions<Settings>()
        //     .Configure<IConfiguration>((settings, configuration) =>
        //     {
        //         configuration.GetSection(Settings.Section).Bind(settings);
        //         System.Console.WriteLine($"Settings: {settings}");
        //     });

        // //var config = builder.GetContext().Configuration;
        // //builder.Services.Configure<Settings>(config.GetSection(Settings.Section));
    }

}
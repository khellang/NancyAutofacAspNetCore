using System;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Bootstrappers.Autofac;
using Nancy.Owin;

namespace AspNetCoreNancy
{
    public class Startup
    {
        public static Task Main(string[] args)
        {
            return CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(web => web.UseStartup<Startup>())
                .UseAutofac();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddNancy<Bootstrapper>();
        }

        // Because of the call to UseAutofac above, this method will be
        // called by the ASP.NET Core host to set up the Autofac container.
        public void ConfigureContainer(ContainerBuilder builder)
        {
            // TODO: Use Autofac-specific APIs, i.e. scanning, modules, etc., to configure the container.
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseNancy();
        }
    }

    public class Bootstrapper : AutofacNancyBootstrapper
    {
        public Bootstrapper(ILifetimeScope container)
        {
            // This is the Autofac container created and injected by ASP.NET Core.
            Container = container;
        }

        private ILifetimeScope Container { get; }

        protected override ILifetimeScope GetApplicationContainer()
        {
            // We don't want Nancy to create its own container instance,
            // just return the one we received from ASP.NET Core.
            return Container;
        }
    }

    public sealed class HomeModule : NancyModule
    {
        public HomeModule(ILogger<HomeModule> logger)
        {
            Get("/", _ =>
            {
                // The logger is injected from ASP.NET Core, just to prove that it works :)
                logger.LogInformation("It works!");

                return "Hello from Nancy and Autofac on ASP.NET Core 3.0!";
            });
        }
    }

    public static class HelperExtensions
    {
        public static IHostBuilder UseAutofac(this IHostBuilder builder)
        {
            return builder.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        }

        public static IServiceCollection AddNancy<TBootstrapper>(this IServiceCollection services)
            where TBootstrapper : class, INancyBootstrapper
        {
            return services.AddSingleton<INancyBootstrapper, TBootstrapper>();
        }

        public static IApplicationBuilder UseNancy(this IApplicationBuilder app, Action<NancyOptions> configure = null)
        {
            var options = new NancyOptions
            {
                // We try to pull it from the application container in order for it to get the ILifetimeScope injected.
                Bootstrapper = app.ApplicationServices.GetService<INancyBootstrapper>()
            };

            configure?.Invoke(options);

            // Because synchronous IO is disabled by default in ASP.NET Core 3.0 and
            // some paths in Nancy still requires synchronous IO, we have to enable it.
            app.UseSynchronousIO();

            // Because there's still no proper ASP.NET Core middleware
            // for Nancy, we have to wrap it in the OWIN adapter.
            return app.UseOwin(owin => owin.UseNancy(options));
        }

        private static IApplicationBuilder UseSynchronousIO(this IApplicationBuilder app)
        {
            return app.Use((ctx, next) =>
            {
                var feature = ctx.Features.Get<IHttpBodyControlFeature>();

                if (feature is null)
                {
                    return next();
                }

                feature.AllowSynchronousIO = true;

                return next();
            });
        }
    }
}

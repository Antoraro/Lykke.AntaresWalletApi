using System;
using System.Globalization;
using AntaresWalletApi.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.GrpcServices;
using AntaresWalletApi.Infrastructure.Authentication;
using AntaresWalletApi.Modules;
using Autofac;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Swisschain.Sdk.Server.Common;
using Swisschain.Sdk.Server.Swagger;

namespace AntaresWalletApi
{
    public sealed class Startup
    {
        public IConfiguration ConfigRoot { get; }
        public AppConfig Config { get; }

        public Startup(IConfiguration configuration)
        {
            ConfigRoot = configuration;
            Config = ConfigRoot.Get<AppConfig>();
        }

         public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers(options =>
                {
                    options.Filters.Add(new ProducesAttribute("application/json"));
                })
                .AddNewtonsoftJson(options =>
                {
                    var namingStrategy = new CamelCaseNamingStrategy();

                    options.SerializerSettings.Converters.Add(new StringEnumConverter(namingStrategy));
                    options.SerializerSettings.NullValueHandling = NullValueHandling.Include;
                    options.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
                    options.SerializerSettings.Culture = CultureInfo.InvariantCulture;
                    options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
                    options.SerializerSettings.MissingMemberHandling = MissingMemberHandling.Error;
                    options.SerializerSettings.ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = namingStrategy
                    };
                });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = ApplicationInformation.AppName, Version = "v1" });
                c.EnableXmsEnumExtension();
                c.MakeResponseValueTypesRequired();
                c.AddJwtBearerAuthorization();
            });
            services.AddSwaggerGenNewtonsoftSupport();

            services.AddGrpc(options =>
            {
                options.Interceptors.Add<LykkeTokenInterceptor>();
            });

            services.AddGrpcReflection();

            services.AddCors(options => options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyHeader();
                builder.AllowAnyMethod();
                builder.AllowAnyOrigin();
            }));

            services.AddSingleton(Config);

            services
                .AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                });

            ConfigureServicesExt(services);
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterModule(new AutofacModule(Config));
            builder.RegisterModule(new AutoMapperModule());
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime applicationLifetime)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseCors();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGrpcService<MonitoringService>();
                endpoints.MapGrpcService<ApiService>();
                endpoints.MapGrpcReflectionService();
            });

            app.UseSwagger(c => c.RouteTemplate = "api/{documentName}/swagger.json");
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("../../api/v1/swagger.json", "API V1");
                c.RoutePrefix = "swagger/ui";
            });
        }

        private void ConfigureServicesExt(IServiceCollection services)
        {
            services.AddSingleton(Config);
            services.AddMemoryCache();

            services.AddHttpClient(HttpClientNames.WalletApiV1, client =>
            {
                client.BaseAddress = new Uri(Config.Services.WalletApiv1Url);
            });
        }
    }
}

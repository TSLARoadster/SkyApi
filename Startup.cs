using System;
using System.IO;
using System.Reflection;
using Coflnet.Sky.Api.Models;
using Coflnet.Sky.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OpenTracing;
using OpenTracing.Util;
using Prometheus;
using Coflnet.Sky.Commands.Shared;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using StackExchange.Redis;
using AspNetCoreRateLimit;
using AspNetCoreRateLimit.Redis;
using Coflnet.Sky.Api.Services;
using Coflnet.Sky.Filter;

namespace Coflnet.Sky.Api
{
    /// <summary>
    /// The asp.net core startup code
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Creates a new instance of <see cref="Startup"/>
        /// </summary>
        /// <param name="configuration"></param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        
        private IConfiguration Configuration { get; }
        private static string CORS_PLICY_NAME = "defaultCorsPolicy";


        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services"></param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson();
            services.AddSwaggerGenNewtonsoftSupport();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "SkyApi", Version = "v1", 
                    Description = "Notes: PET, RUNE and POTION item tags (somtimes called ids) are expanded to include the type, eg PET_LION."
                                + " All other Tags match with from hypixel and can be found via the search endpoint." });
                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath, true);
            });
            services.AddCors(o =>
            {
                o.AddPolicy(CORS_PLICY_NAME, p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
            });

            services.AddJaeger(0.001, 60);
            services.AddScoped<PricesService>();
            services.AddSingleton<PremiumService>();
            services.AddSingleton<AuctionService>();
            services.AddDbContext<HypixelContext>();
            services.AddTransient<KatService>();

            services.AddResponseCaching();
            services.AddResponseCompression();
            var redisOptions = ConfigurationOptions.Parse(Configuration["REDIS_HOST"]);

            services.AddStackExchangeRedisCache(options =>
            {
                options.ConfigurationOptions = redisOptions;
                options.InstanceName = "SampleInstance";
            });
            services.AddSingleton<IConnectionMultiplexer>(provider => ConnectionMultiplexer.Connect(redisOptions));

            // Rate limiting 
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));
            services.AddRedisRateLimiting();
            services.AddSingleton<IIpPolicyStore, DistributedCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, DistributedCacheRateLimitCounterStore>();
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
            services.AddCoflService();
            services.AddSingleton<Sky.Mayor.Client.Api.IElectionPeriodsApi>(a =>
            {
                return new Sky.Mayor.Client.Api.ElectionPeriodsApi(Configuration["MAYOR_BASE_URL"]);
            });

            services.AddSingleton<TfmService>();
            services.AddSingleton<ModDescriptionService>();
            services.AddSingleton<FilterEngine>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            app.UseExceptionHandler(errorApp =>
            {
                ErrorHandler.Add(logger, errorApp, "api");
            });
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwagger();
            app.UseSwagger(a =>
            {
                a.RouteTemplate = "api/swagger/{documentName}/swagger.json";
            });
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "SkyApi v1");
                c.RoutePrefix = "api";
            });

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors(CORS_PLICY_NAME);

            app.UseAuthorization();

            app.UseResponseCaching();
            app.UseResponseCompression();
            app.UseIpRateLimiting();



            app.UseEndpoints(endpoints =>
            {
                endpoints.MapMetrics();
                endpoints.MapControllers();
            });
        }
    }
}

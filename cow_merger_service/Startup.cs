using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace cow_merger_service
{
    public class Startup
    {
        private ILogger _logger;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        private bool CheckConfiguration()
        {
            bool isOk = true;
            if (!Directory.Exists(Configuration["Settings:WorkingDirectory"]))
            {
                _logger.Log(LogLevel.Critical, $"WorkingDirectory:{Environment.NewLine}{Configuration["Settings: WorkingDirectory"]}{Environment.NewLine} does not exists, bye!");
                isOk = false;
            }

            if (!Directory.Exists(Configuration["Settings:OriginalImageDirectory"]))
            {
                _logger.Log(LogLevel.Critical, $"OriginalImageDirectory:{Environment.NewLine}{Configuration["Settings:OriginalImageDirectory"]}{Environment.NewLine} does not exists, bye!");
                isOk = false;
            }

            if (!Directory.Exists(Configuration["Settings:DestinationDirectory"]))
            {
                _logger.Log(LogLevel.Critical, $"DestinationDirectory:{Environment.NewLine}{Configuration["Settings:DestinationDirectory"]}{Environment.NewLine} does not exists, bye!");
                isOk = false;
            }
            _logger.Log(LogLevel.Information,
                $"workingDirectory: {Path.GetFullPath(Configuration["Settings:WorkingDirectory"])}");
            _logger.Log(LogLevel.Information,
                $"originalImageDirectory: {Path.GetFullPath(Configuration["Settings:OriginalImageDirectory"])}");
            _logger.Log(LogLevel.Information,
                $"destinationDirectory: {Path.GetFullPath(Configuration["Settings:DestinationDirectory"])}");
            return isOk;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<SessionManager>();

            services.AddControllers(options =>
                {
                    //options.InputFormatters.Insert(0, new RawRequestBodyFormatter());
                })
                .AddJsonOptions(options => { options.JsonSerializerOptions.IncludeFields = true; });
            ;


            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "cow_merger_service", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifeTime,
            ILogger<Startup> logger)
        {
        
            _logger = logger;
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "cow_merger_service v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });


            if (!CheckConfiguration()) lifeTime.StopApplication();
        }
    }
}
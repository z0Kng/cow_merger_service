using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.IO;
using Microsoft.Extensions.Logging;


namespace cow_merger_service
{
    public class Startup
    {
        private ILogger _logger;
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private bool CheckConfiguration()
        {
            bool isOk = true;
            DirectoryInfo d = Directory.CreateDirectory("test");
            Console.WriteLine(d.FullName);
            if (!Directory.Exists(Configuration["Settings:WorkingDirectory"]))
            {
                _logger.Log(LogLevel.Critical, "WorkingDirectory does not exists, bye!");
                isOk = false;
            }
            if (!Directory.Exists(Configuration["Settings:OriginalImageDirectory"]))
            {
                _logger.Log(LogLevel.Critical, "OriginalImageDirectory does not exists, bye!");
                isOk = false;
            }
            if (!Directory.Exists(Configuration["Settings:DestinationDirectory"]))
            {
                _logger.Log(LogLevel.Critical, "DestinationDirectory does not exists, bye!");
                isOk = false;
            }
            _logger.Log(LogLevel.Information, $"workingDirectory: {Configuration["Settings:WorkingDirectory"]}");
            _logger.Log(LogLevel.Information, $"originalImageDirectory: {Configuration["Settings:OriginalImageDirectory"]}");
            _logger.Log(LogLevel.Information, $"destinationDirectory: {Configuration["Settings:DestinationDirectory"]}");
            return isOk;

        }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<SessionManager>();

               services.AddControllers(options =>
            {
                options.InputFormatters.Insert(0, new RawRequestBodyFormatter());
            });
            
           
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "cow_merger_service", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifeTime, ILogger<Startup> logger)
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

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            if (!CheckConfiguration())
            {
                lifeTime.StopApplication();
            }
        }
    }
}

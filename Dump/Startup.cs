using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Audit.Core.Providers;
using Audit.Elasticsearch.Providers;
using Audit.SqlServer;
using Audit.SqlServer.Providers;
using Audit.WebApi;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Dump
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Dump", Version = "v1" });
            });
            services.AddMetricsTrackingMiddleware();

            // Audit.Core.Configuration.DataProvider = new SqlDataProvider()
            // {
            //     ConnectionString = "data source=(localdb)\\MSSQLLocalDB;initial catalog=Audit;integrated security=true;",
            //     Schema = "dbo",
            //     TableName = "Event",
            //     IdColumnName = "EventId",
            //     JsonColumnName = "JsonData",
            //     LastUpdatedDateColumnName = "LastUpdatedDate",
            //     CustomColumns = new List<CustomColumn>()
            //     {
            //         new CustomColumn("EventType", ev => ev.EventType),
            //         new CustomColumn("Duration", ev => ev.Duration),
            //         new CustomColumn("StartDate", ev => ev.StartDate),
            //         new CustomColumn("EndDate", ev => ev.EndDate),
            //         new CustomColumn("RequestFrom", ev => ev.Environment.UserName),
            //         new CustomColumn("IPAddress", ev => ev.GetWebApiAuditAction().IpAddress),
            //         new CustomColumn("RequestBody", ev => JsonConvert.SerializeObject(ev.GetWebApiAuditAction().RequestBody)),
            //         new CustomColumn("ResponseBody", ev => JsonConvert.SerializeObject(ev.GetWebApiAuditAction().ResponseBody)),
            //         new CustomColumn("RequestId", ev => ev.GetWebApiAuditAction().TraceId),
            //
            //     }
            // };
            
            // Audit.Core.Configuration.DataProvider = new Audit.MongoDB.Providers.MongoDataProvider()
            // {
            //     ConnectionString = "mongodb://localhost:27017",
            //     Database = "Audit",
            //     Collection = "Event",
            //     SerializeAsBson = true,
            // };
            
            Audit.Core.Configuration.DataProvider = new ElasticsearchDataProvider()
            {
                ConnectionSettings = new AuditConnectionSettings(new Uri("http://localhost:9200")),
                IndexBuilder = ev => "auditlog",
                IdBuilder = ev => Guid.NewGuid()
            };
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dump v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseMetricsAllMiddleware();

            app.UseAuditMiddleware(_ => _
                    .WithEventType("{verb}:{url}")
                    .IncludeRequestBody(true)
                    .IncludeResponseBody(true)
                    .IncludeHeaders(true)
                    .IncludeResponseHeaders(true));

            app.UseAuthorization();

            app.Use(async (context, next) => {  // <----
                context.Request.EnableBuffering(); // or .EnableRewind();
                await next();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}

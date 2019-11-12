using System;
using System.Linq;
using System.Net.Http;
using Ags.ResourceProxy.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Extensions.Hosting;

namespace Ags.ResourceProxy.Web {
	public class Startup {
		public Startup(IConfiguration configuration) {
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services) {

            services.AddSingleton<IProxyConfigService, ProxyConfigService>((proxyConfigService) =>
            {
                var agsProxyConfig = new ProxyConfigService(proxyConfigService.GetService<IWebHostEnvironment>(),
                    "proxy.config.json");
                agsProxyConfig.Config.ServerUrls.ToList().ForEach(su => {
                    services.AddHttpClient(su.Url)
                        .ConfigurePrimaryHttpMessageHandler(h => new HttpClientHandler
                        {
                            AllowAutoRedirect = false,
                            Credentials = agsProxyConfig.GetCredentials(agsProxyConfig.GetProxyServerUrlConfig((su.Url)))
                        });
                });
                return agsProxyConfig;
            });
            services.AddSingleton<IProxyService, ProxyService>();

            services.AddMvc(options => options.EnableEndpointRouting = false).SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
			if (env.IsDevelopment()) {
				app.UseDeveloperExceptionPage();
			} else {
				app.UseExceptionHandler("/Error");
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseStaticFiles();
			app.UseCookiePolicy();

			app.UseWhen(context => {
				return context.Request.Path.Value.ToLower().StartsWith(@"/proxy/proxy.ashx", StringComparison.OrdinalIgnoreCase);
				//&& context.User.Identity.IsAuthenticated; // Add this back in to keep unauthenticated users from utilzing the proxy.
			},
				builder =>
					builder.UseAgsProxyServer(
					app.ApplicationServices.GetService<IProxyConfigService>(),
					app.ApplicationServices.GetService<IProxyService>(),
					app.ApplicationServices.GetService<IMemoryCache>())
				);

			app.UseMvc();
		}
	}
}

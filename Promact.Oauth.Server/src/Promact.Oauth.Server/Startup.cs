﻿using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Promact.Oauth.Server.Data;
using Promact.Oauth.Server.Models;
using Promact.Oauth.Server.Services;
using Promact.Oauth.Server.Seed;
using Promact.Oauth.Server.Repository;
using Promact.Oauth.Server.Data_Repository;
using Promact.Oauth.Server.Repository.ProjectsRepository;
using Promact.Oauth.Server.Repository.ConsumerAppRepository;
using Promact.Oauth.Server.Repository.OAuthRepository;
using Promact.Oauth.Server.Repository.HttpClientRepository;
using System.Net.Http;
using Promact.Oauth.Server.AutoMapper;
using AutoMapper;
using Exceptionless;
using NLog.Extensions.Logging;
using Promact.Oauth.Server.Constants;

namespace Promact.Oauth.Server
{
    public class Startup
    {
        private ILoggerFactory _loggerFactory { get; }
        private IHostingEnvironment _currentEnvironment { get; set; }
        public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            _currentEnvironment = env;
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets();
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();


            _mapperConfiguration = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new AutoMapperProfileConfiguration());
            });

            _loggerFactory = loggerFactory;

        }

        public IConfigurationRoot Configuration { get; }

        private MapperConfiguration _mapperConfiguration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //var builder = new ContainerBuilder();
            //builder.RegisterType<AuthMessageSender>().As<IEmailSender>().InstancePerDependency();
            //builder.RegisterType<AuthMessageSender>().As<ISmsSender>().InstancePerDependency();

            // Add framework services.
            services.AddDbContext<PromactOauthDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<PromactOauthDbContext>()
                .AddDefaultTokenProviders();

            //Register application services
            services.AddScoped<IEnsureSeedData, EnsureSeedData>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<IConsumerAppRepository, ConsumerAppRepository>();
            services.AddScoped(typeof(IDataRepository<>), typeof(DataRepository<>));
            services.AddScoped<IOAuthRepository, OAuthRepository>();
            services.AddScoped<IStringConstant,StringConstant>();
            services.AddScoped<HttpClient>();
            
            services.AddScoped<IHttpClientRepository, HttpClientRepository>();


            services.AddMvc();
            services.AddScoped<CustomAttribute>();
            //.AddJsonOptions(opt =>
            //{
            //    var resolver = opt.SerializerSettings.ContractResolver;
            //    if (resolver != null)
            //    {
            //        var res = resolver as DefaultContractResolver;
            //        res.NamingStrategy = null;  // <<!-- this removes the camelcasing
            //    }
            //});

            // Add application services.
            if (_currentEnvironment.IsDevelopment())
                services.AddTransient<IEmailSender, AuthMessageSender>();
            else if (_currentEnvironment.IsProduction())
                services.AddTransient<IEmailSender, SendGridEmailSender>();
            services.AddTransient<ISmsSender, AuthMessageSender>();

            services.AddOptions();

            // Configure AppSettingUtil using code
            services.Configure<AppSettingUtil>(appSettingUtil =>
            {
                appSettingUtil.PromactOAuthUrl = "http://localhost:35716";
                appSettingUtil.CasualLeave = "14";
                appSettingUtil.SickLeave = "7";
            });

            // Configure MyOptions using config by installing Microsoft.Extensions.Options.ConfigurationExtensions
            services.Configure<AppSettings>(Configuration);

            //Register Mapper
            services.AddSingleton<IMapper>(sp => _mapperConfiguration.CreateMapper());

            services.AddMvc().AddMvcOptions(x => x.Filters.Add(new GlobalExceptionFilter(_loggerFactory)));
        }



        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IEnsureSeedData seeder, IServiceProvider serviceProvider)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            //add NLog to ASP.NET Core
            loggerFactory.AddNLog();
            //needed for non-NETSTANDARD platforms: configure nlog.config in your project root
            env.ConfigureNLog("nlog.config");

            //Call the Seed method in (Seed.EnsureSeedData) to create initial Admin
            seeder.Seed(serviceProvider);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseIdentity();

            // Add Exceptionless Api_key and will be used on project for throwing exception
            app.UseExceptionless(Environment.GetEnvironmentVariable("ExceptionLessApiKey"));
            // Add external authentication middleware below. To configure them please see http://go.microsoft.com/fwlink/?LinkID=532715

            app.UseMvc(routes =>
            {

                routes.MapRoute(
                      name: "Login",
                      template: "Login",
                      defaults: new { controller = "Account", action = "Login" });

                routes.MapRoute(
                    name: "LogOff",
                    template: "LogOff",
                    defaults: new { controller = "Account", action = "LogOff" });

                //routes.MapRoute(
                //        name: "default",
                //         template: "{*.}",
                //     defaults: new { controller = "Home", action = "Index" }
                //     );

                routes.MapRoute(
                    name: "default",
                    //template: "{controller=Account}/{action=Login}");
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}

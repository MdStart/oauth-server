﻿using AutoMapper;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Promact.Oauth.Server.AutoMapper;
using Promact.Oauth.Server.Data;
using Promact.Oauth.Server.Data_Repository;
using Promact.Oauth.Server.Models;
using Promact.Oauth.Server.Repository;
using Promact.Oauth.Server.Repository.ConsumerAppRepository;
using Promact.Oauth.Server.Repository.OAuthRepository;
using Promact.Oauth.Server.Repository.ProjectsRepository;
using Promact.Oauth.Server.Seed;
using Promact.Oauth.Server.Services;
using System;
using System.Linq;
using System.Collections.Generic;
using Promact.Oauth.Server.Constants;
using Moq;
using Promact.Oauth.Server.Models.ApplicationClasses;
using System.Threading.Tasks;
using Promact.Oauth.Server.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Authentication;

namespace Promact.Oauth.Server.Tests
{
    public class BaseProvider
    {
        public IServiceProvider serviceProvider { get; set; }

        private MapperConfiguration _mapperConfiguration { get; set; }
        private readonly Mock<IEmailUtil> _emailUtilMock;
        private readonly IStringConstant _stringConstant;
        private readonly IUserRepository _userRepository;
        private readonly Mock<IEmailSender> _mockEmailService;

        public BaseProvider()
        {

            var randomString = Guid.NewGuid().ToString();

            _mapperConfiguration = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new AutoMapperProfileConfiguration());
            });


            var services = new ServiceCollection();
            services.AddEntityFrameworkInMemoryDatabase();
            services.Configure<AppSettingUtil>(x =>
            {
                x.CasualLeave = 14;
                x.PromactErpUrl =  _stringConstant.PromactErpUrlForTest;
                x.PromactOAuthUrl = _stringConstant.PromactErpUrlForTest;
                x.SickLeave = 7;
            });

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<PromactOauthDbContext>()
                .AddDefaultTokenProviders();
            services.AddScoped<IEnsureSeedData, EnsureSeedData>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<IConsumerAppRepository, ConsumerAppRepository>();
            services.AddScoped<IOAuthRepository, OAuthRepository>();
            services.AddScoped<IStringConstant, StringConstant>();
            services.AddScoped(typeof(IDataRepository<>), typeof(DataRepository<>));

            //Register Mapper
            services.AddSingleton<IMapper>(sp => _mapperConfiguration.CreateMapper());
            services.AddTransient<IEmailSender, AuthMessageSender>();
            
            services.AddDbContext<PromactOauthDbContext>(options => options.UseInMemoryDatabase(randomString), ServiceLifetime.Transient);
            var httpClientMock = new Mock<IHttpClientService>();
            var httpClientMockObject = httpClientMock.Object;
            services.AddScoped(x => httpClientMock);
            services.AddScoped(x=>httpClientMockObject);

            // Http Context mocking
            var authenticationManagerMock = new Mock<AuthenticationManager>();
            var httpContextMock = new Mock<HttpContext>();
            httpContextMock.Setup(x => x.Authentication).Returns(authenticationManagerMock.Object);
            var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContextMock.Object);
            var httpContextMockObject = httpContextAccessorMock.Object;
            services.AddScoped(x => httpContextAccessorMock);
            services.AddScoped(x => httpContextMockObject);

            services.AddScoped(x => httpClientMockObject);
            
            //Register email util mock
            var emailUtilMock = new Mock<IEmailUtil>();
            var emailUtilMockObject = emailUtilMock.Object;
            services.AddScoped(x => emailUtilMock);
            services.AddScoped(x => emailUtilMockObject);

            //Register email service mock
            var emailServiceMock = new Mock<IEmailSender>();
            var emailServiceMockObject = emailServiceMock.Object;
            services.AddScoped(x => emailServiceMock);
            services.AddScoped(x => emailServiceMockObject);


            serviceProvider = services.BuildServiceProvider();
            RoleSeedFake(serviceProvider);

            _emailUtilMock = serviceProvider.GetService<Mock<IEmailUtil>>();
            _mockEmailService = serviceProvider.GetService<Mock<IEmailSender>>();
            _stringConstant = serviceProvider.GetService<IStringConstant>();
            _userRepository = serviceProvider.GetService<IUserRepository>();

            //mock SendEmail and GetEmailTemplateForUserDetail methods
            _mockEmailService.Setup(x => x.SendEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));
            _emailUtilMock.Setup(x => x.GetEmailTemplateForUserDetail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

        }

        #region Public Method(s)

        public void RoleSeedFake(IServiceProvider serviceProvider)
        {
            var _db = serviceProvider.GetService<PromactOauthDbContext>();
            var _stringConstant = serviceProvider.GetService<IStringConstant>();
            if (!_db.Roles.Any())
            {
                List<IdentityRole> roles = new List<IdentityRole>();
                roles.Add(new IdentityRole { Name = _stringConstant.Employee, NormalizedName = _stringConstant.NormalizedName });
                roles.Add(new IdentityRole { Name = _stringConstant.Admin, NormalizedName = _stringConstant.NormalizedSecond });

                foreach (var role in roles)
                {
                    _db.Roles.Add(role);
                }
                _db.SaveChanges();
            }
        }

        /// <summary>
        /// This method is used to create new user.
        /// </summary>
        /// <returns></returns>
        public async Task<string> CreateMockAndUserAsync()
        {
            UserAc _testUser = new UserAc()
            {
                Email = _stringConstant.UserName,
                FirstName = _stringConstant.FirstName,
                LastName = _stringConstant.LastName,
                IsActive = true,
                SlackUserId = _stringConstant.SlackUserId,
                UserName = _stringConstant.UserName,
                SlackUserName = _stringConstant.SlackUserName,
                JoiningDate = DateTime.UtcNow,
                RoleName = _stringConstant.Employee
            };
            return await _userRepository.AddUserAsync(_testUser, _stringConstant.RawFirstNameForTest);
        }
        
        #endregion
    }

}

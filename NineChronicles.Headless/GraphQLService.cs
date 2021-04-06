using System;
using System.Collections.Generic;
using System.Security.Claims;
using GraphQL.Server;
using GraphQL.Server.Ui.Playground;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Middleware;
using NineChronicles.Headless.Options;
using NineChronicles.Headless.Properties;
using Serilog;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless
{
    public class GraphQLService
    {
        public const string UserPolicyKey = "UserPolicy";
        
        public const string AdminPolicyKey = "AdminPolicy";

        public const string UserContextPrivateKeyKey = "UserPrivateKey";

        public const string NoCorsPolicyName = "AllowAllOrigins";

        public const string NoCorsKey = "noCors";

        private GraphQLNodeServiceProperties GraphQlNodeServiceProperties { get; }

        public GraphQLService(GraphQLNodeServiceProperties properties)
        {
            GraphQlNodeServiceProperties = properties;
        }

        public IHostBuilder Configure(IHostBuilder hostBuilder, StandaloneContext standaloneContext)
        {
            var listenHost = GraphQlNodeServiceProperties.GraphQLListenHost;
            var listenPort = GraphQlNodeServiceProperties.GraphQLListenPort;

            return hostBuilder.ConfigureWebHostDefaults(builder =>
            {
                builder.UseStartup<GraphQLStartup>();
                builder.ConfigureAppConfiguration(
                    (context, builder) =>
                    {
                        var dictionary = new Dictionary<string, string>();

                        if (!(GraphQlNodeServiceProperties.AdminPassphrase is null))
                        {
                            dictionary[nameof(AuthenticationMutationOptions.AdminPassphrase)] =
                                GraphQlNodeServiceProperties.AdminPassphrase;
                        }

                        if (GraphQlNodeServiceProperties.NoCors)
                        {
                            dictionary[NoCorsKey] = string.Empty;
                        }

                        builder.AddInMemoryCollection(dictionary);
                    });
                builder.ConfigureServices(
                    services => services.AddSingleton(standaloneContext)
                        .AddSingleton(standaloneContext.KeyStore)
                        .AddSingleton<IMiner>(standaloneContext.NineChroniclesNodeService!));
                builder.UseUrls($"http://{listenHost}:{listenPort}/");
            });
        }

        internal class GraphQLStartup
        {
            public GraphQLStartup(IConfiguration configuration)
            {
                Configuration = configuration;
            }

            public IConfiguration Configuration { get; }

            public void ConfigureServices(IServiceCollection services)
            {
                if (!(Configuration[NoCorsKey] is null))
                {
                    services.AddCors(
                        options =>
                            options.AddPolicy(
                                NoCorsPolicyName,
                                builder =>
                                    builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
                }

                services.AddTransient<AuthenticationValidationMiddleware>();

                services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie();

                services.AddSession(options =>
                {
                    options.Cookie.Name = ".NineChronicles.Session";
                    options.Cookie.IsEssential = true;
                    options.Cookie.HttpOnly = true;
                });

                services.AddDistributedMemoryCache();

                services.AddHealthChecks();

                services.AddControllers();
                services.AddGraphQL(
                        (options, provider) =>
                        {
                            options.EnableMetrics = true;
                            options.UnhandledExceptionDelegate = context =>
                            {
                                Console.Error.WriteLine(context.Exception.ToString());
                                Console.Error.WriteLine(context.ErrorMessage);
                            };
                        })
                    .AddSystemTextJson()
                    .AddWebSockets()
                    .AddDataLoader()
                    .AddGraphTypes(typeof(StandaloneSchema))
                    .AddLibplanetExplorer<NCAction>()
                    .AddUserContextBuilder<UserContextBuilder>()
                    .AddGraphQLAuthorization(
                        options =>
                        {
                            options.AddPolicy(
                                UserPolicyKey, 
                                p => p.RequireClaim(ClaimTypes.Role, "User"));

                            options.AddPolicy(
                                AdminPolicyKey,
                                p => p.RequireClaim(ClaimTypes.Role, "Admin"));
                        });
                services.AddGraphTypes();
            }

            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }

                if (Configuration[NoCorsKey] is null)
                {
                    app.UseCors();
                }
                else
                {
                    app.UseCors("AllowAllOrigins");
                }

                app.UseSession();
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseCookiePolicy();

                app.UseMiddleware<AuthenticationValidationMiddleware>();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapHealthChecks("/health-check");
                });

                // WebSocket으로 운영합니다.
                app.UseWebSockets();
                app.UseGraphQLWebSockets<StandaloneSchema>("/graphql");
                app.UseGraphQL<StandaloneSchema>("/graphql");

                // Prints 
                app.UseMiddleware<GraphQLSchemaMiddleware<StandaloneSchema>>("/schema.graphql");

                // /ui/playground 옵션을 통해서 Playground를 사용할 수 있습니다.
                app.UseGraphQLPlayground(new GraphQLPlaygroundOptions
                {
                    RequestCredentials = RequestCredentials.Include,
                });
            }
        }
    }
}

using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NineChronicles.Headless.Properties;
using System.Net;
using System.Reactive.Subjects;
using Grpc.Core;
using Grpc.Net.Client;
using Lib9c.Formatters;
using Lib9c.Renderer;
using Libplanet.Action;
using Libplanet.Blockchain.Renderers;
using Libplanet.Blockchain.Renderers.Debug;
using Libplanet.Headless;
using Libplanet.Headless.Hosting;
using Libplanet.Net;
using MagicOnion.Server;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Nekoyume.Action;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.RPC.Shared.Exceptions;
using Serilog;
using Serilog.Events;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder UseNineChroniclesNode(
            this IHostBuilder builder,
            NineChroniclesNodeServiceProperties properties,
            StandaloneContext context
        )
        {
            return builder.ConfigureServices(services =>
            {
                var blockRenderer = new BlockRenderer();
                var actionRenderer = new ActionRenderer();
                var exceptionRenderer = new ExceptionRenderer();
                var nodeStatusRenderer = new NodeStatusRenderer();

                services.AddSingleton<BlockRenderer>(blockRenderer);
                services.AddSingleton<ActionRenderer>(actionRenderer);
                services.AddSingleton<ExceptionRenderer>(exceptionRenderer);
                services.AddSingleton<NodeStatusRenderer>(nodeStatusRenderer);
                services.AddSingleton<LoggedActionRenderer<NCAction>>(provider =>
                    new LoggedActionRenderer<NCAction>(
                        provider.GetRequiredService<ActionRenderer>(),
                        provider.GetRequiredService<ILogger>(),
                        LogEventLevel.Debug));
                services.AddSingleton<LoggedRenderer<NCAction>>(provider =>
                    new LoggedRenderer<NCAction>(
                        provider.GetRequiredService<BlockRenderer>(),
                        provider.GetRequiredService<ILogger>(),
                        LogEventLevel.Debug));
                services.AddSingleton<IEnumerable<IRenderer<NCAction>>>(provider =>
                {
                    var renderers = new List<IRenderer<NCAction>>();
                    if (properties.Libplanet.Render)
                    {
                        renderers.Add(provider.GetRequiredService<BlockRenderer>());
                        renderers.Add(provider.GetRequiredService<LoggedActionRenderer<NCAction>>());
                    }
                    else if (properties.Libplanet.LogActionRenders)
                    {
                        renderers.Add(provider.GetRequiredService<BlockRenderer>());
                        // The following "nullRenderer" does nothing.  It's just for filling
                        // the LoggedActionRenderer<T>() constructor's parameter:
                        IActionRenderer<NCAction> nullRenderer =
                            new AnonymousActionRenderer<NCAction>();
                        renderers.Add(
                            new LoggedActionRenderer<NCAction>(
                                nullRenderer,
                                Log.Logger,
                                LogEventLevel.Debug
                            )
                        );
                    }
                    else
                    {
                        renderers.Add(provider.GetRequiredService<LoggedRenderer<NCAction>>());
                    }

                    if (properties.StrictRender)
                    {
                        Log.Debug(
                            $"Strict rendering is on. Add StrictRenderer.");
                        renderers.Add(new ValidatingActionRenderer<NCAction>(onError: exc =>
                            provider.GetRequiredService<ExceptionRenderer>().RenderException(
                                RPCException.InvalidRenderException,
                                exc.Message.Split("\n")[0]
                            )
                        ));
                    }

                    return renderers;
                });
                services.AddSingleton<LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>>(
                    provider => properties.Libplanet);
                services.AddLibplanet<PolymorphicAction<ActionBase>>(
                    properties.Libplanet,
                    properties.Libplanet.GenesisBlock,
                    null);
                services.AddSingleton(provider =>
                {
                    return new ActionEvaluationPublisher(
                        provider.GetRequiredService<BlockRenderer>(),
                        provider.GetRequiredService<ActionRenderer>(),
                        provider.GetRequiredService<ExceptionRenderer>(),
                        provider.GetRequiredService<NodeStatusRenderer>(),
                        IPAddress.Loopback.ToString(),
                        0,
                        new RpcContext
                        {
                            RpcRemoteSever = false
                        }
                    );
                });
                services.AddSingleton<Subject<DifferentAppProtocolVersionEncounter>>();
                services.AddSingleton<Subject<Notification>>();
                services.AddSingleton<Subject<NodeException>>();
                services.AddSingleton<Subject<MonsterCollectionState>>();
                services.AddSingleton<Subject<MonsterCollectionStatus>>();
                services.AddSingleton<Subject<NodeStatusType>>();
                services.AddSingleton<Subject<PreloadState>>();
                services.AddSingleton<AgentDictionary>();
            });
        }

        public static IHostBuilder UseNineChroniclesRPC(
            this IHostBuilder builder,
            RpcNodeServiceProperties properties
        )
        {
            var context = new RpcContext
            {
                RpcRemoteSever = properties.RpcRemoteServer
            };

            return builder
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_ => context);
                    services.AddGrpc(options =>
                    {
                        options.MaxReceiveMessageSize = null;
                    });
                    services.AddMagicOnion();
                    services.AddSingleton(provider =>
                    {
                        return new ActionEvaluationPublisher(
                            provider.GetRequiredService<BlockRenderer>(),
                            provider.GetRequiredService<ActionRenderer>(),
                            provider.GetRequiredService<ExceptionRenderer>(),
                            provider.GetRequiredService<NodeStatusRenderer>(),
                            IPAddress.Loopback.ToString(),
                            properties.RpcListenPort,
                            context
                        );
                    });
                    var resolver = MessagePack.Resolvers.CompositeResolver.Create(
                        NineChroniclesResolver.Instance,
                        StandardResolver.Instance
                    );
                    var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);
                    MessagePackSerializer.DefaultOptions = options;
                })
                .ConfigureWebHostDefaults(hostBuilder =>
                {
                    hostBuilder.ConfigureKestrel(options =>
                    {
                        options.ListenAnyIP(properties.RpcListenPort, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http2;
                        });
                    });
                });
        }
    }
}

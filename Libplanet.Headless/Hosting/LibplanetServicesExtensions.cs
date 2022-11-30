using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using Bencodex;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Net;
using Libplanet.Store;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Libplanet.Headless.Hosting;

public static class LibplanetServicesExtensions
{
    public static IServiceCollection AddLibplanet<T>(
        this IServiceCollection services,
        LibplanetNodeServiceProperties<T> configuration,
        Block<T> genesisBlock,
        IImmutableSet<Currency> nativeTokens)
        where T : IAction, new()
    {
        services.AddSingleton(Log.Logger);
        services.AddSingleton<IBlockPolicy<T>>(
            _ => new BlockPolicy<T>(nativeTokens: nativeTokens)
        );
        services.AddSingleton<IStagePolicy<T>>(
            _ => new VolatileStagePolicy<T>()
        );
        services.AddSingleton<IStore>(_ =>
        {
            IStore store = new RocksDBStore.RocksDBStore(configuration.StorePath);
            return configuration.NoReduceStore
                ? store
                : new ReducedStore(store);
        });
        services.AddSingleton<IStateStore>(_ => new TrieStateStore(
            new RocksDBStore.RocksDBKeyValueStore(
                Path.Combine(configuration.StorePath!, "states")
            )
        ));
        services.AddSingleton(_ =>
        {
            if (!(configuration.GenesisBlock is null))
            {
                return configuration.GenesisBlock;
            }
            else if (!string.IsNullOrEmpty(configuration.GenesisBlockPath))
            {
                byte[] rawBlock;
                if (File.Exists(Path.GetFullPath(configuration.GenesisBlockPath)))
                {
                    rawBlock = File.ReadAllBytes(Path.GetFullPath(configuration.GenesisBlockPath));
                }
                else
                {
                    var uri = new Uri(configuration.GenesisBlockPath);
                    using var client = new HttpClient();
                    rawBlock = client.GetByteArrayAsync(uri).Result;
                }
                var blockDict = (Bencodex.Types.Dictionary)new Codec().Decode(rawBlock);
                return BlockMarshaler.UnmarshalBlock<T>(blockDict);
            }

            throw new InvalidOperationException();
        });
        services.AddSingleton(provider =>
        {
            return new Swarm<T>(
                provider.GetRequiredService<BlockChain<T>>(),
                configuration.SwarmPrivateKey,
                configuration.AppProtocolVersion,
                host: configuration.Host,
                listenPort: configuration.Port
            );
        });
        services.AddSingleton<BlockChain<T>>();
        services.AddSingleton(_ => configuration);

        services.AddSingleton(provider =>
            new SwarmService<T>(
                provider.GetRequiredService<Swarm<T>>(),
                configuration.Peers.ToArray()
            )
        );
        services.AddHostedService<SwarmService<T>>(provider => provider.GetRequiredService<SwarmService<T>>());

        if (configuration.SwarmPrivateKey is { } minerPrivateKey)
        {
            services.AddHostedService(provider =>
                new MinerService<T>(
                    provider.GetRequiredService<BlockChain<T>>(),
                    minerPrivateKey
                )
            );
        }

        return services;
    }
}

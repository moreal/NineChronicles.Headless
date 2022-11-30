using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Explorer.Interfaces;
using Libplanet.Headless.Hosting;
using Libplanet.Store;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless
{
    public class BlockChainContext : IBlockChainContext<NCAction>
    {
        private readonly BlockChain<NCAction> _blockChain;
        private readonly IStore _store;
        private readonly SwarmService<NCAction> _swarmService;

        public BlockChainContext(BlockChain<NCAction> blockChain, IStore store, SwarmService<NCAction> swarmService)
        {
            _blockChain = blockChain;
            _store = store;
            _swarmService = swarmService;
        }

        public bool Preloaded => _swarmService.PreloadFinished;
        public BlockChain<PolymorphicAction<ActionBase>>? BlockChain => _blockChain;
        public IStore? Store => _store;
    }
}

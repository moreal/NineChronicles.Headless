using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Libplanet;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;

namespace NineChronicles.Headless;

public class AgentDictionary : ConcurrentDictionary<Address,
    (ReplaySubject<MonsterCollectionStatus> statusSubject, ReplaySubject<MonsterCollectionState> stateSubject, ReplaySubject<string> balanceSubject)>
{
}

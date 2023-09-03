using System;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Tx;
using NineChronicles.Headless.GraphTypes;
using GlobExpressions;

namespace NineChronicles.Headless.GraphQL.Queries
{
    public class TransactionHeadlessQuery<T> : ObjectGraphType
        where T : IAction, new()
    {
        public TransactionHeadlessQuery(StandaloneContext standaloneContext)
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<TransactionType<T>>>>>(
                name: "ncTransactions",
                arguments: new QueryArguments(
                    new QueryArgument<StringGraphType>
                    {
                        Name = "actionTypeIdPattern",
                        Description = "Glob pattern for action's type_id",
                    }
                ),
                resolve: context =>
                {
                    string actionTypeIdPattern = context.GetArgument<string>("actionTypeIdPattern");

                    return standaloneContext.BlockChain
                        .GetAllTransactions()
                        .Where(tx => Glob.IsMatch(tx.Actions.Single().TypeId.ToString(), actionTypeIdPattern))
                        .Select(tx => new Transaction<T>(tx));
                }
            );
        }
    }
}

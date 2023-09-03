using System;
using System.Linq;
using System.Text.RegularExpressions;
using GraphQL;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Tx;
using NineChronicles.Headless.GraphTypes;

namespace NineChronicles.Headless.GraphQL.Queries
{
    public class TransactionQuery<T> : ObjectGraphType
        where T : IAction, new()
    {
        public TransactionQuery(StandaloneContext standaloneContext)
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
                    Regex pattern = new Regex(actionTypeIdPattern);

                    return standaloneContext.BlockChain
                        .GetAllTransactions()
                        .Where(tx => pattern.IsMatch(tx.Actions.Single().TypeId.ToString()))
                        .Select(tx => new Transaction<T>(tx));
                }
            );
        }
    }
}

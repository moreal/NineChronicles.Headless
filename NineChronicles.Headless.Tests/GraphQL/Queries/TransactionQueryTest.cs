using System;
using System.Linq;
using Xunit;
using NineChronicles.Headless.GraphQL.Queries;
using NineChronicles.Headless.Tests.Common;

namespace NineChronicles.Headless.Tests.GraphQL.Queries
{
    public class TransactionQueryTest
    {
        private TransactionQuery _transactionQuery;

        public TransactionQueryTest()
        {
            _transactionQuery = new TransactionQuery();
        }

        [Fact]
        public void NcTransactions_WithGlobPattern_FiltersCorrectly()
        {
            // Arrange
            var globPattern = "*Action*";
            var transactions = TestData.GetTransactions();

            // Act
            var result = _transactionQuery.NcTransactions(transactions, globPattern);

            // Assert
            Assert.All(result, tx => Assert.Contains(globPattern, tx.Action.TypeId));
        }

        // Existing test cases...
    }
}

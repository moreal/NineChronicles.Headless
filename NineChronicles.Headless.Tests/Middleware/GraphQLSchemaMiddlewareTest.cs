using System.IO;
using System.Text;
using System.Threading.Tasks;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Middleware;
using Xunit;

namespace NineChronicles.Headless.Tests.Middleware
{
    public class GraphQLSchemaMiddlewareTest
    {
        private const string GraphQlSchemaMiddlewareEndpoint = "/schema.graphql";

        private readonly GraphQLSchemaMiddleware<Schema> _middleware;

        public GraphQLSchemaMiddlewareTest()
        {
            _middleware = new GraphQLSchemaMiddleware<Schema>(context => Task.CompletedTask, GraphQlSchemaMiddlewareEndpoint);
        }

        [Fact]
        public async Task InvokeAsync()
        {
            var httpContext = new DefaultHttpContext
            {
                Request =
                {
                    Path = GraphQlSchemaMiddlewareEndpoint,
                },
                Response =
                {
                    Body = new MemoryStream(),
                }
            };
            await _middleware.InvokeAsync(httpContext, new Schema
            {
                Query = new ObjectGraphType
                {
                    Name = "Fruit",
                }
            });

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(httpContext.Response.Body);
            Assert.Equal("schema {\r\n  query: Fruit\r\n}\r\n\r\ntype Fruit {\r\n\r\n}\r\n", await reader.ReadToEndAsync());
        }
    }
}

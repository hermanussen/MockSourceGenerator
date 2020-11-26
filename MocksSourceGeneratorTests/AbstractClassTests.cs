using Xunit;
using Xunit.Abstractions;

namespace MocksSourceGeneratorTests
{
    public class AbstractClassTests : TestsBase
    {
        public AbstractClassTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ShouldGenerateConstructor()
        {
            string source = @"using System;
namespace Example
{
    abstract class ExternalSystemServiceBase
    {
        public string PassedVal { get; private set; }

        public ExternalSystemServiceBase(string val)
        {
            this.PassedVal = val;
        }
    }

    class Test
    {
        public static string RunTest()
        {
            var valString = ""someval"";
            var mock = (ExternalSystemServiceBase) new MyMock(valString);
            return $""{mock.PassedVal}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("someval", RunTest(compilation));
        }

        [Fact]
        public void ShouldGenerateForAbstractClass()
        {
            string source = @"using System;
namespace Example
{
    abstract class ExternalSystemServiceBase
    {
        internal abstract int Add(int operand1, int operand2);
        internal virtual int Subtract(int operand1, int operand2)
        {
            throw new NotImplementedException();
        }
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (ExternalSystemServiceBase) new MyMock()
                {
                    MockAdd = (o1, o2) => o1 + o2,
                    MockSubtract = (o1, o2) => o1 - o2
                };
            return $""{mock.Add(5, 7)} {mock.Subtract(19, 7)}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("12 12", RunTest(compilation));
        }

        [Fact]
        public void ShouldGenerateForAbstractClassWithProtectedInternal()
        {
            string source = @"using System;
namespace Example
{
    abstract class ExternalSystemServiceBase
    {
        protected internal abstract int Add(int operand1, int operand2);
        protected internal abstract int SomeProperty { get; set;}
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (ExternalSystemServiceBase) new MyMock()
                {
                    MockAdd = (o1, o2) => o1 + o2
                };
            return $""{mock.Add(5, 7)}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("12", RunTest(compilation));
        }

        [Fact]
        public void ShouldGenerateForAbstractClassVisibility()
        {
            string source = @"using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http
{
    // Need to make a more accessible version of SendAsync so we can test it
    public partial class HandlerMock
    {
        public global::System.Threading.Tasks.Task<global::System.Net.Http.HttpResponseMessage> SendAsyncPublic(global::System.Net.Http.HttpRequestMessage request)
        {
            return SendAsync(request, global::System.Threading.CancellationToken.None);
        }
    }
}

namespace Example
{
    class Test
    {
        public static string RunTest()
        {
            var mock = (HttpMessageHandler) new HandlerMock()
                {
                    MockSendAsync = (request, cancellationToken) => Task.FromResult(new HttpResponseMessage()
                    {
                        StatusCode = HttpStatusCode.Accepted,
                        Content = null,
                        RequestMessage = null
                    })
                };
            return $""{((HandlerMock) mock).SendAsyncPublic(null).Result.StatusCode}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("Accepted", RunTest(compilation));
        }
    }
}

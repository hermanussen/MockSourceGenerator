using Xunit;
using Xunit.Abstractions;

namespace MocksSourceGeneratorTests
{
    public class RegularClassTests : TestsBase
    {
        public RegularClassTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ShouldGenerateForRegularClass()
        {
            string source = @"using System;
namespace Example
{
    class ExternalSystemService
    {
        internal int Add(int operand1, int operand2)
        {
            throw new NotImplementedException();
        }

        internal virtual int Subtract(int operand1, int operand2)
        {
            throw new NotImplementedException();
        }
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (ExternalSystemService) new MyMock()
                {
                    MockSubtract = (o1, o2) => o1 - o2
                };
            return $""{mock.Subtract(19, 7)}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("12", RunTest(compilation));
        }
    }
}

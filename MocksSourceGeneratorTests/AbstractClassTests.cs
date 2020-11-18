using System;
using System.Collections.Generic;
using System.Text;
using SourceGeneratorTests;
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
    }
}

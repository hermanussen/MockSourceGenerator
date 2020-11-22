using Xunit;
using Xunit.Abstractions;

namespace MocksSourceGeneratorTests
{
    public class NestedTypeTests : TestsBase
    {
        public NestedTypeTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ShouldGenerateForNestedInterface()
        {
            string source = @"using System;
namespace Example
{
    class Outer
    {
        public interface IExternalSystemService
        {
            int Add(int operand1, int operand2);
        }
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (Outer.IExternalSystemService) new MyMock()
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
        public void ShouldGenerateForNestedClass()
        {
            string source = @"using System;
namespace Example
{
    public class Outer
    {
        public class ExternalSystemService
        {
            public virtual int Add(int operand1, int operand2)
            {
                throw new NotImplementedException();
            }
        }
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (Outer.ExternalSystemService) new MyMock()
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
    }
}

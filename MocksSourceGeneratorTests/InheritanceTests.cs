using Xunit;
using Xunit.Abstractions;

namespace MocksSourceGeneratorTests
{
    public class InheritanceTests : TestsBase
    {
        public InheritanceTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ShouldGenerateMethodsFromBaseClass()
        {
            string source = @"using System;
namespace Example
{
    interface IExternalSystemServiceBase
    {
        int Multiply(int operand1, int operand2);
    }   

    interface IExternalSystemService : IExternalSystemServiceBase
    {
        int Add(int operand1, int operand2);
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (IExternalSystemService) new MyMock()
                {
                    MockMultiply = (o1, o2) => o1 * o2
                };
            return $""{mock.Multiply(5, 7)}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("35", RunTest(compilation));
        }

        [Fact]
        public void ShouldGeneratePropertiesFromBaseClass()
        {
            string source = @"using System;
namespace Example
{
    interface IExternalSystemServiceBase
    {
        string SomeProperty { get; set; }
    }   

    interface IExternalSystemService : IExternalSystemServiceBase
    {
        int Multiply(int operand1, int operand2);
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (IExternalSystemService) new MyMock()
                {
                    SomeProperty = ""someval""
                };
            return $""{mock.SomeProperty}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("someval", RunTest(compilation));
        }

        [Fact]
        public void ShouldGenerateWithoutConflicts()
        {
            string source = @"using System;
namespace Example
{
    interface IExternalSystemService
    {
        int Add(int operand1, int operand2);
    }   

    abstract class ExternalSystemService : IExternalSystemService
    {
        public virtual int Add(int operand1, int operand2)
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

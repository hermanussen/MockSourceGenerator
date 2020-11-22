using Xunit;
using Xunit.Abstractions;

namespace MocksSourceGeneratorTests
{
    public class PropertiesTests : TestsBase
    {
        public PropertiesTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ShouldGeneratePropertyMock()
        {
            string source = @"using System;
namespace Example
{
    interface IExternalSystemService
    {
        string SomeProperty { get; set; }
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
    }
}

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

        [Fact]
        public void ShouldGeneratePropertyMockForReferenceType()
        {
            string source = @"using System;
namespace Example
{
    interface IModel
    {
        string Prop { get; set; }
    }

    interface IExternalSystemService
    {
        IModel SomeProperty { get; set; }
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (IExternalSystemService) new MyMock()
                {
                    SomeProperty = (IModel) new ModelMock()
                        {
                            Prop = ""someval""
                        }
                };
            return $""{mock.SomeProperty.Prop}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("someval", RunTest(compilation));
        }

        [Fact]
        public void ShouldGenerateIfOnlyGetIsAccessible()
        {
            string source = @"using System;
namespace Example
{
    internal abstract class ExternalSystemServiceBase
    {
        protected virtual string SomeProp { get; }
    }   

    internal class ExternalSystemService : ExternalSystemServiceBase
    {
        internal string GetPropVal()
        {
            return this.SomeProp;
        }
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (ExternalSystemService) new MyMock()
                {
                    MockSomeProp = ""someval""
                };
            return $""{mock.GetPropVal()}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("someval", RunTest(compilation));
        }
    }
}

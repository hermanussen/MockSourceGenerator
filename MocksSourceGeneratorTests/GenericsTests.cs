using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MocksSourceGeneratorTests
{
    public class GenericsTests : TestsBase
    {
        public GenericsTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ShouldGenerateWithCorrectGenericType()
        {
            string source = @"using System;
namespace Example
{
    interface IModel
    {
        string ModelName { get; }
    }

    interface IAnimalModel : IModel
    {
    }

    interface IExternalSystemService<T>
    {
        T SomeGenericMethod(T a);
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (IExternalSystemService<IModel>) new MyMock()
                {
                    MockSomeGenericMethod = (a) => a
                };
            return $""{mock.SomeGenericMethod((IAnimalModel) new AnimalMock() { ModelName = ""Elephant"" }).ModelName}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("Elephant", RunTest(compilation));
        }
    }
}

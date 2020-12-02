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

        public static async Task Blah()
        {
            await Task.Delay(1000).ConfigureAwait(false);
            
        }

        [Fact]
        public void ShouldGenerateWithCorrectGenericTypeAdvanced()
        {
            string source = @"using System;
using System.Threading.Tasks;
namespace Example
{
    public interface IModelMapper<T>
    {
        public string ModelStr { get; }
    }
    
    public class Parameter
    {
        public string Name { get; private set; }
    }

    public interface IAgent<T>
    {
        Task<T> Get(string resourceName, Parameter[] parameters = null, bool returnMockData = false);
    }

    public class Agent<T> : IAgent<T>
    {
        private readonly IModelMapper<T> modelMapper;

        public Agent(IModelMapper<T> modelMapper)
        {
            this.modelMapper = modelMapper;
        }

        public async Task<T?> Get(string resourceName, Parameter[] parameters = null, bool returnMockData = false)
        {
            return default(T);
        }
    }

    class Test
    {
        public static string RunTest()
        {
            var modelMapperMock = (IModelMapper<string>) new ModelMapperMock()
                {
                    ModelStr = ""returnval""
                };
            var mock = (Agent<string>) new MyMock(modelMapperMock)
                {
                    MockGet = (a, b, c) => modelMapperMock.ModelStr
                };
            var task = Task.Run<string>(async () => await ((MyMock) mock).Get(""resource"", new Parameter[0], false));

            string result = task.Result;
            
            return $""{result}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("returnval", RunTest(compilation));
        }
    }
}

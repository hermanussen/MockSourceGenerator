using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MocksSourceGeneratorTests
{
    public class DefaultIfNotMockedTests : TestsBase
    {
        public DefaultIfNotMockedTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ShouldGenerateWhenFlagIsSet()
        {
            string source = @"using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace Example
{
    public abstract class IExternalSystemService
    {
        public abstract int Add(int operand1, int operand2);
        public abstract IEnumerable<bool> GetList();
        public abstract bool GetBool();
        public virtual async Task<decimal> GetAsync(int i)
        {
            await Task.Delay(2);
            return 20;
        }
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (IExternalSystemService) new MyMock()
                {
                    ReturnDefaultIfNotMocked = true
                };
            var task = Task.Run<decimal>(async () => await mock.GetAsync(0));
            return $""{mock.Add(5, 7)}-{mock.GetList()}-{mock.GetBool()}-{task.Result}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("0--False-0", RunTest(compilation));
        }
    }
}

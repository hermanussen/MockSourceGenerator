using MocksSourceGeneratorTests;
using Xunit;
using Xunit.Abstractions;

namespace MocksSourceGeneratorTests
{
    public class BasicUsageTests : TestsBase
    {
        public BasicUsageTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ShouldGenerate()
        {
            string source = @"using System;
namespace Example
{
    interface IExternalSystemService
    {
        int Add(int operand1, int operand2);
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (IExternalSystemService) new MyMock()
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
        public void ShouldGenerateWithoutParameters()
        {
            string source = @"using System;
namespace Example
{
    interface IExternalSystemService
    {
        string GetSomeString();
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (IExternalSystemService) new MyMock()
                {
                    MockGetSomeString = () => ""mockstring""
                };
            return mock.GetSomeString();
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("mockstring", RunTest(compilation));
        }

        [Fact]
        public void ShouldGenerateWithComplexInput()
        {
            string source = @"using System;
using System.Collections.Generic;
using System.IO;
namespace Example
{
    interface IExternalSystemService
    {
        void AddSomething(IList<MemoryStream> memoryStreams);
    }

    class Test
    {
        public static string RunTest()
        {
            var list = new List<MemoryStream>();
            list.Add(new MemoryStream());
            list.Add(new MemoryStream());

            int amount = 0;
            var mock = (IExternalSystemService) new MyMock()
                {
                    MockAddSomething = (m) => { amount = m.Count; }
                };
            mock.AddSomething(list);
            return $""{amount}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("2", RunTest(compilation));
        }

        [Fact]
        public void ShouldGenerateWithoutParametersAndWithoutReturn()
        {
            string source = @"using System;
namespace Example
{
    interface IExternalSystemService
    {
        void NotMuch();
    }

    class Test
    {
        public static string RunTest()
        {
            bool called = false;
            var mock = (IExternalSystemService) new MyMock()
                {
                    MockNotMuch = () => { called = true; }
                };
            mock.NotMuch();
            return $""{called}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal(true.ToString(), RunTest(compilation));
        }

        [Fact]
        public void ShouldGenerateWithMultipleNamespaces()
        {
            string source = @"using System;
namespace Example1
{
    interface IExternalSystemService
    {
        int Add(int operand1, int operand2);
    }
}

namespace Example2
{
    interface IExternalSystemService2
    {
        int Add(int operand1, int operand2);
    }
}

namespace Example
{
    using Example1;
    using Example2;

    class Test
    {
        public static string RunTest()
        {
            var mock = (IExternalSystemService) new MyMock()
                {
                    MockAdd = (o1, o2) => o1 + o2
                };
            var mock2 = (IExternalSystemService2) new MySecondMock()
                {
                    MockAdd = (o1, o2) => o1 + o2
                };
            return $""{mock.Add(5, 7)} {mock2.Add(5, 7)}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("12 12", RunTest(compilation));
        }
    }
}

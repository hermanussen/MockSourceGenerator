using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MocksSourceGeneratorTests
{
    public class HistoryTests : TestsBase
    {
        public HistoryTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ShouldHaveHistory()
        {
            string source = @"using System;
using System.Globalization;
using Xunit;

namespace Example
{
    interface IExternalSystemService
    {
        int Add(int operand1, int operand2);
        int Subtract(int operand1, int operand2);
        CultureInfo GetSomeCulture();
        void SetSomeOtherCulture(CultureInfo culture);
        void Unused();
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (IExternalSystemService) new MyMock()
                {
                    MockAdd = (o1, o2) => o1 + o2,
                    MockSubtract = (o1, o2) => o1 - o2,
                    MockGetSomeCulture = () => CultureInfo.GetCultureInfo(""en""),
                    MockSetSomeOtherCulture = (c) => {},
                    MockUnused = () => {}
                };
            var result = $""{mock.Add(5, 7)} {mock.Subtract(22, 5)} {mock.Add(74, 6)} {mock.GetSomeCulture()}"";
            mock.SetSomeOtherCulture(CultureInfo.GetCultureInfo(""en""));

            Assert.Collection(
                ((MyMock) mock).HistoryEntries,
                i => Assert.Equal(""Add"", i.MethodName),
                i => Assert.Equal(""Subtract"", i.MethodName),
                i => Assert.Equal(""Add"", i.MethodName),
                i => Assert.Equal(""GetSomeCulture"", i.MethodName),
                i => Assert.Equal(""SetSomeOtherCulture"", i.MethodName)
            );

            Assert.Collection(
                ((MyMock) mock).HistoryEntries,
                i => Assert.Equal(""Add(5, 7)"", i.ToString()),
                i => Assert.Equal(""Subtract(22, 5)"", i.ToString()),
                i => Assert.Equal(""Add(74, 6)"", i.ToString()),
                i => Assert.Equal(""GetSomeCulture()"", i.ToString()),
                i => Assert.Equal(""SetSomeOtherCulture(en)"", i.ToString())
            );

            return result;
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("12 17 80 en", RunTest(compilation));
        }
    }
}

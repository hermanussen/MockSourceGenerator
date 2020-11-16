using ConsoleApp;
using Xunit;

namespace ConsoleAppTests
{
    public class SomeServiceTest
    {
        [Theory]
        [InlineData(1, 2, 3)]
        public void ShouldAddUsingExternalService(int operand1, int operand2, int result)
        {
            var mock = new IExternalSystemServiceMock
            {
                MockAdd = (operand1, operand2) => result
            };
            var calculated = new SomeService(mock)
                .AddUsingExternalService(operand1, operand2);
            Assert.Equal(result, calculated);
        }
    }
}

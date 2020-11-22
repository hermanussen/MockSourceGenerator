using ExampleApp;
using Xunit;

namespace ExampleAppTests
{
    public class SomeServiceTest
    {
        [Theory]
        [InlineData(1, 2, 3)]
        public void ShouldAddUsingExternalService(int operand1, int operand2, int result)
        {
            // The generator will generate an appropriate mock class
            // - It will use whatever name you choose for the mock class, as long as it ends with "Mock"
            // - The cast is important; the generator will know what to mock based on the type used
            var mock = (ExternalSystemService) new MyMock
                {
                    MockAdd = (o1, o2) => result
                };
            var calculated = new SomeService(mock)
                .AddUsingExternalService(operand1, operand2);
            Assert.Equal(result, calculated);
        }
    }
}

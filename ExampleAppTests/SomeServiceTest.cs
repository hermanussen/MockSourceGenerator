using ExampleApp;
using Xunit;

namespace ExampleAppTests
{
    public class SomeServiceTest
    {
        [Fact]
        public void TestCase()
        {
            // The generator will generate an appropriate mock class
            // - It will use whatever name you choose for the mock class, as long as it ends with "Mock"
            // - The cast is important; the generator will know what to mock based on the type used
            var mock = (IExternalSystemService) new MyMock
            {
                MockAdd = (o1, o2) => 5
            };
            Assert.Equal(5, mock.Add(2, 3));
        }

        [Fact]
        public void TestCase2()
        {
            // The following does not provide a mock implementation for Add(...)
            var mock = (IExternalSystemService) new MyMock
            {
                ReturnDefaultIfNotMocked = true
            };
            Assert.Equal(0, mock.Add(2, 3)); // 0 is the default int value
        }

        [Fact]
        public void TestCase3()
        {
            var mock = (IExternalSystemService)new MyMock
            {
                MockAdd = (o1, o2) => 5
            };

            Assert.Equal(5, mock.Add(2, 3));
            Assert.Equal(5, mock.Add(1, 4));

            // Check if the Add(...) method is called twice, with given parameters (optional)
            Assert.Collection(
                ((MyMock) mock).HistoryEntries,
                i => Assert.Equal("Add(2, 3)", i.ToString()),
                i => Assert.Equal("Add(1, 4)", i.ToString()));
        }
    }
}

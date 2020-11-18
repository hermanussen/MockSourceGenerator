# MockSourceGenerator
A C# mocking library that generates mocks at compile-time using a source generator.

# Example usage

```csharp
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
```
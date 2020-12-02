# MockSourceGenerator

[![publish to nuget](https://github.com/hermanussen/MockSourceGenerator/workflows/publish%20to%20nuget/badge.svg)](https://github.com/hermanussen/MockSourceGenerator/actions) [![Nuget](https://img.shields.io/nuget/v/MockSourceGenerator)](https://www.nuget.org/packages/MockSourceGenerator/) [![Nuget](https://img.shields.io/nuget/dt/MockSourceGenerator?label=nuget%20downloads)](https://www.nuget.org/packages/MockSourceGenerator/) [![Twitter URL](https://img.shields.io/twitter/url?style=social&url=https%3A%2F%2Ftwitter.com%2Fknifecore%2F)](https://twitter.com/knifecore)

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

By default, you must mock all methods that are called during the test. If you don't mock something, an exception will be thrown. You can opt-out of this by setting `ReturnDefaultIfNotMocked` to true. Anything that is not mocked, will then return the default value of the return type.

```csharp
[Fact]
public void TestCase()
{
    // The following does not provide a mock implementation for Add(...)
    var mock = (IExternalSystemService) new MyMock
        {
            ReturnDefaultIfNotMocked = true
        };
    Assert.Equal(0, mock.Add(2, 3)); // 0 is the default int value
}
```

# How to install

This can only be used in projects that use C# 9.0 (or higher), such as .NET 5.0 projects.

Run the following command in the NuGet package manager console
```
Install-Package MockSourceGenerator
```
or using the .NET cli
```
dotnet add package MockSourceGenerator
```
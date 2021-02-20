# MockSourceGenerator

[![publish to nuget](https://github.com/hermanussen/MockSourceGenerator/workflows/publish%20to%20nuget/badge.svg)](https://github.com/hermanussen/MockSourceGenerator/actions) [![Nuget](https://img.shields.io/nuget/v/MockSourceGenerator)](https://www.nuget.org/packages/MockSourceGenerator/) [![Nuget](https://img.shields.io/nuget/dt/MockSourceGenerator?label=nuget%20downloads)](https://www.nuget.org/packages/MockSourceGenerator/) [![Twitter URL](https://img.shields.io/twitter/url?style=social&url=https%3A%2F%2Ftwitter.com%2Fknifecore%2F)](https://twitter.com/knifecore)

A C# mocking library that generates mocks at compile-time using a source generator.

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

# How to use

> Learn how to use the mock source generator in just 5 easy steps.

## Step 1
Let's say we need a mock that implements the following interface:
```csharp
public interface IExternalSystemService
{
    public int Add(int operand1, int operand2);
}
```

## Step 2
We start by creating a test:
```csharp
[Fact]
public void TestCase()
{
    var mock = new MyMock();
    // ...
}
```

That won't compile, because the `MyMock` type doesn't exist. But be patient, because it will be there soon. Note that the type name must end with `Mock`. Apart from that, you can choose any name you want (e.g.: `ExternalSystemServiceMock` or `GeneratedServiceMock`).

## Step 3
Let's add a cast, to indicate to the generator what type to mock:
```csharp
[Fact]
public void TestCase()
{
    var mock = (IExternalSystemService) new MyMock();
    // ...
}
```

This will actually compile! The source generator will generate a class that can be used to start mocking things.

## Step 4
```csharp
[Fact]
public void TestCase()
{
    var mock = (IExternalSystemService) new MyMock
        {
            MockAdd = (o1, o2) => 5    
        };
    // ...
}
```

For every member that can be mocked, another member will be generated with a `Mock` prefix. In this case, `MockAdd` allows you to set an implementation. In this case I chose for the method to always return 5, but anything can be done there (including throwing an exception if something unexpected is passed as an argument).

I used an [object initializer](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/how-to-initialize-objects-by-using-an-object-initializer) here, though this is not required. I personally like doing it this way, because it makes it clear how the mock will function in just 1 statement.

## Step 5
```csharp
[Fact]
public void TestCase()
{
    var mock = (IExternalSystemService) new MyMock
        {
            MockAdd = (o1, o2) => 5    
        };
    Assert.Equal(5, mock.Add(2, 3));
}
```

Now the test is done. It's not very useful, as it just tests the mock itself. But I hope you can see how easy it is to use this method to inject your mock into a class that is being tested.

There are more things you can do, which I'll explain below. But I hope this basic tutorial helps you get started.

# Default mock implementations

By default, you must mock all methods that are called during the test. If you don't mock something, an exception will be thrown. You can opt-out of this by setting `ReturnDefaultIfNotMocked` to true. Anything that is not mocked, will then return the default value of the return type.

```csharp
[Fact]
public void TestCase2()
{
    // The following does not provide a mock implementation for Add(...)
    var mock = (IExternalSystemService) new MyMock
        {
            ReturnDefaultIfNotMocked = true
        };
    Assert.Equal(0, mock.Add(-2, 2)); // 0 is the default int value
}
```

# Verification

You can also verify if the mocked methods are called (and in which order, with which parameters, if you want).
```csharp
[Fact]
public void TestCase3()
{
    var mock = (IExternalSystemService) new MyMock
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
```

# Now what?

Well, I think this is a good place to start from. If you are excited about this or have any ideas on how to improve things, please let me know on [Twitter](https://twitter.com/knifecore)!

Some links that you might like:
- [Short Twitter thread with comparison to Moq](https://twitter.com/knifecore/status/1329092901263998978)
- [Learn about more source generators and how they work](https://github.com/amis92/csharp-source-generators)
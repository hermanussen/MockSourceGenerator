﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using SourceGeneratorTests;
using Xunit;
using Xunit.Abstractions;

namespace MocksSourceGeneratorTests
{
    public class DuplicateTests : TestsBase
    {
        public DuplicateTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ShouldGenerateOnlyOnce()
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
            var mock2 = (IExternalSystemService) new MyMock()
                {
                    MockAdd = (o1, o2) => o1 + o2
                };
            return $""{mock.Add(5, 7)} {mock2.Add(10, 14)}"";
        }
    }
}";
            var compilation = GetGeneratedOutput(source);

            Assert.Equal("12 24", RunTest(compilation));
        }

        [Fact]
        public void ShouldGenerateErrorForNamingConflicts()
        {
            string source = @"using System;
namespace Example
{
    interface IExternalSystemService
    {
        int Add(int operand1, int operand2);
    }

    interface ISecondExternalSystemService
    {
        int Subtract(int operand1, int operand2);
    }

    class Test
    {
        public static string RunTest()
        {
            var mock = (IExternalSystemService) new MyMock()
                {
                    MockAdd = (o1, o2) => o1 + o2
                };
            var mock2 = (ISecondExternalSystemService) new MyMock()
                {
                    MockSubtract = (o1, o2) => o1 - o2
                };
            return $""{mock.Add(5, 7)} {mock2.Subtract(22, 10)}"";
        }
    }
}";
            List<Diagnostic> diagnostics = new List<Diagnostic>();
            GetGeneratedOutput(source, diagnostics);
            Assert.NotEmpty(diagnostics);
            Assert.Equal("The type 'Example.MyMock' cannot be used for mocking 'global::Example.ISecondExternalSystemService', as it was already used to mock 'global::Example.IExternalSystemService'", diagnostics.First().GetMessage());
        }
    }
}

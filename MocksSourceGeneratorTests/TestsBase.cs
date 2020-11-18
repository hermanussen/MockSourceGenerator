using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;
using Xunit.Abstractions;

namespace SourceGeneratorTests
{
    public class TestsBase
    {
        private ITestOutputHelper _output;
        private static List<MetadataReference> _metadataReferences;
        private static readonly object Lock = new object();

        public TestsBase(ITestOutputHelper output)
        {
            _output = output;
        }

        private static List<MetadataReference> MetadataReferences
        {
            get
            {
                lock (Lock)
                {
                    if (_metadataReferences == null)
                    {
                        _metadataReferences = new List<MetadataReference>();
                        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                        foreach (var assembly in assemblies)
                        {
                            if (!assembly.IsDynamic)
                            {
                                _metadataReferences.Add(MetadataReference.CreateFromFile(assembly.Location));
                            }
                        }
                    }
                }

                return _metadataReferences;
            }
        }

        protected string RunTest(Compilation compilation, List<Diagnostic> diagnostics = null)
        {
            using var memoryStream = new MemoryStream();
            EmitResult result = compilation.Emit(memoryStream);

            if (result.Success)
            {
                memoryStream.Seek(0, SeekOrigin.Begin);
                Assembly assembly = Assembly.Load(memoryStream.ToArray());

                Type testClassType = assembly.GetType("Example.Test");
                var stringResult = testClassType?.GetMethod("RunTest")?.Invoke(null, new object[0]) as string;
                _output.WriteLine($"Generated test output:\r\n===\r\n{stringResult}\r\n===\r\n");
                return stringResult;
            }

            if (diagnostics == null)
            {
                Assert.False(true,
                    $"Compilation did not succeed: {string.Join("\r\n", result.Diagnostics.Select(d => $"{Enum.GetName(typeof(DiagnosticSeverity), d.Severity)} ({d.Location}) - {d.GetMessage()}"))}");
            }
            else
            {
                diagnostics.AddRange(result.Diagnostics);
            }

            return null;
        }

        protected Compilation GetGeneratedOutput(string source, List<Diagnostic> diagnostics = null)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var references = MetadataReferences;

            var compilation = CSharpCompilation.Create("TestImplementation", new [] { syntaxTree }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            ISourceGenerator generator = new MocksSourceGenerator.MocksSourceGenerator();

            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generateDiagnostics);

            if (diagnostics == null)
            {
                Assert.False(generateDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), "Failed: " + generateDiagnostics.FirstOrDefault()?.GetMessage());
            }
            else
            {
                diagnostics.AddRange(generateDiagnostics);
            }

            string output = outputCompilation.SyntaxTrees.Last().ToString();
            Assert.NotNull(output);

            _output.WriteLine($"Generated code:\r\n===\r\n{output}\r\n===\r\n");

            return outputCompilation;
        }
    }
}
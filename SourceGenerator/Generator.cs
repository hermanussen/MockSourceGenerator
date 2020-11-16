using System;
using Microsoft.CodeAnalysis;

namespace SourceGenerator
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                var source = @"using System;
using ConsoleApp;

public class IExternalSystemServiceMock : IExternalSystemService
{
    public Func<int,int,int> MockAdd { get; set; }
    public int Add(int operand1, int operand2)
    {
        if (MockAdd == null)
        {
            throw new NotImplementedException(""Method was called, but no mock implementation was provided"");
        }

        return MockAdd(operand1, operand2);
    }
}";

                context.AddSource("GeneratedMocks.gen.cs", source);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}

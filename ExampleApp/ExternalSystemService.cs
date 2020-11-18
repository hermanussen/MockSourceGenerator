using System;
using System.Collections.Generic;
using System.IO;

namespace ExampleApp
{
    public class ExternalSystemService : IExternalSystemService
    {
        public int Add(int operand1, int operand2)
        {
            throw new NotImplementedException("This is not actually implemented, that's why we should Mock it");
        }
    }

    public interface IExternalSystemService
    {
        public int Add(int operand1, int operand2);
    }
}

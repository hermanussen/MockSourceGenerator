using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var externalSystemService = new ExternalSystemService();
            new SomeService(externalSystemService)
                .AddUsingExternalService(1, 3);
        }
    }
}

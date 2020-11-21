namespace ExampleApp
{
    class Program
    {
        static void Main()
        {
            var externalSystemService = new ExternalSystemService();
            new SomeService(externalSystemService)
                .AddUsingExternalService(1, 3);
        }
    }
}

namespace ExampleApp
{
    public class SomeService
    {
        private readonly IExternalSystemService _externalSystemService;

        public SomeService(IExternalSystemService externalSystemService)
        {
            _externalSystemService = externalSystemService;
        }

        public int AddUsingExternalService(int operand1, int operand2)
        {
            return _externalSystemService.Add(operand1, operand2);
        }
    }
}

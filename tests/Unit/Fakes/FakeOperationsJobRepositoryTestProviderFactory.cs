using IUMP.Tests.Integration.Operations;

namespace IUMP.Tests.Integration.Operations;

public sealed class FakeOperationsJobRepositoryTestProviderFactory :
    IOperationsJobRepositoryTestProviderFactory
{
    public OperationsJobRepositoryFixture Create()
    {
        var fake = new IUMP.Tests.Unit.Fakes.FakeOperationsRepositories();
        return new OperationsJobRepositoryFixture(fake, fake);
    }
}

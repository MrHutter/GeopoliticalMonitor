using GeopoliticalMonitor.Domain;

namespace GeopoliticalMonitor.Domain.Tests;

public class DomainProjectTests
{
    [Fact]
    public void DomainAssemblyIsAvailable()
    {
        Assert.Equal("GeopoliticalMonitor.Domain", typeof(DomainAssemblyMarker).Assembly.GetName().Name);
    }
}

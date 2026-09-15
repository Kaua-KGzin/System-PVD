namespace Archlab.Backend.Hardware;

public interface IScaleService
{
    Task<decimal> ReadWeightAsync(CancellationToken ct = default);
}

public sealed class VirtualMockScaleService(ILogger<VirtualMockScaleService> logger) : IScaleService
{
    private static decimal _simulatedWeight = 0.500m;

    public void SetSimulatedWeight(decimal weight) => _simulatedWeight = Math.Max(0, weight);

    public Task<decimal> ReadWeightAsync(CancellationToken ct = default)
    {
        logger.LogInformation("VIRTUAL SCALE: Read weight {Weight:F3} kg", _simulatedWeight);
        return Task.FromResult(_simulatedWeight);
    }
}

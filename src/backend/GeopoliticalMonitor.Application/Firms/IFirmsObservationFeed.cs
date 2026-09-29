namespace GeopoliticalMonitor.Application.Firms;

public interface IFirmsObservationFeed
{
    Task<FirmsObservationBatch> FetchAsync(
        FirmsSource source,
        FirmsBoundingBox bounds,
        CancellationToken cancellationToken = default);
}

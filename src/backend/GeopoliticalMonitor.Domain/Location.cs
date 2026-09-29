namespace GeopoliticalMonitor.Domain;

public sealed record Location
{
    public Location(double latitude, double longitude, string displayName, string? countryCode = null, string? region = null)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        Latitude = latitude;
        Longitude = longitude;
        DisplayName = displayName;
        CountryCode = string.IsNullOrWhiteSpace(countryCode) ? null : countryCode;
        Region = string.IsNullOrWhiteSpace(region) ? null : region;
    }

    public double Latitude { get; }
    public double Longitude { get; }
    public string DisplayName { get; }
    public string? CountryCode { get; }
    public string? Region { get; }
}
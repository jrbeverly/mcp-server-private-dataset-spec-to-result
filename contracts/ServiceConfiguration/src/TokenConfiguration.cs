namespace ServiceConfiguration;

public sealed record TokenConfiguration
{
    public required string IssuedToken { get; init; }
    public string? HeaderName { get; init; }

    public const string SectionName = "Auth";

    public static TokenConfiguration FromEnvironment()
    {
        var token = Environment.GetEnvironmentVariable("AUTH_TOKEN")
            ?? throw new InvalidOperationException("AUTH_TOKEN is required");

        return new TokenConfiguration
        {
            IssuedToken = token,
            HeaderName = Environment.GetEnvironmentVariable("AUTH_HEADER_NAME") ?? "X-Api-Token"
        };
    }
}

namespace QueryService.Domain.Exceptions;

public abstract class DomainException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }

    protected DomainException(int statusCode, string title, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string entityType, string id)
        : base(404, "Not Found", $"{entityType} with ID '{id}' was not found") { }

    public NotFoundException(string message)
        : base(404, "Not Found", message) { }
}

public class ForbiddenException : DomainException
{
    public ForbiddenException(string message)
        : base(403, "Forbidden", message) { }
}

public class BadRequestException : DomainException
{
    public BadRequestException(string message)
        : base(400, "Bad Request", message) { }
}

public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message)
        : base(401, "Unauthorized", message) { }
}

public class VersionNotAvailableException : DomainException
{
    public VersionNotAvailableException(string version)
        : base(404, "Version Not Available", $"Dataset version '{version}' is not available") { }
}

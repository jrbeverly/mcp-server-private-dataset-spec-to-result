using ProcessingService.Models;

namespace ProcessingService.Pipeline;

public static class ValidationStep
{
    public static ValidationResult Validate(IReadOnlyList<CanonicalEntity> entities)
    {
        var errors = new List<string>();

        if (entities.Count == 0)
        {
            errors.Add("Processing produced zero entities — input may be empty or malformed");
            return new ValidationResult(false, errors);
        }

        // Check for duplicate entity IDs
        var duplicateIds = entities
            .GroupBy(e => e.EntityId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
            errors.Add($"Duplicate entity IDs found: {string.Join(", ", duplicateIds.Take(5))}");

        // Check for missing names
        var unnamedCount = entities.Count(e => e.Name == "Unnamed" || string.IsNullOrWhiteSpace(e.Name));
        if (unnamedCount > entities.Count * 0.5)
            errors.Add($"{unnamedCount} of {entities.Count} entities have no name — input column mapping may be incorrect");

        // Check for entities with no data
        var emptyCount = entities.Count(e => e.Properties.Count == 0 && e.Metrics.Count == 0);
        if (emptyCount == entities.Count)
            errors.Add("All entities have zero properties and zero metrics — no data was extracted");
        // Validate no division by zero produced invalid metrics (decimal cannot represent NaN/Infinity)
        var zeroDivisions = entities
            .SelectMany(e => e.Metrics)
            .Where(kv => kv.Value == decimal.Zero && kv.Key.EndsWith("rate", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (zeroDivisions.Count > 0)
            errors.Add($"{zeroDivisions.Count} rate metrics are zero — possible data issue");

        return errors.Count == 0
            ? new ValidationResult(true, Array.Empty<string>())
            : new ValidationResult(false, errors);
    }
}

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);

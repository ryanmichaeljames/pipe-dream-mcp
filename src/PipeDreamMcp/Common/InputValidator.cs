namespace PipeDream.Mcp.Common;

/// <summary>
/// Validates input parameters for MCP tool calls
/// </summary>
public static class InputValidator
{
    /// <summary>
    /// Validate entity name format
    /// </summary>
    public static void ValidateEntityName(string entity)
    {
        if (string.IsNullOrWhiteSpace(entity))
            throw new ArgumentException("Entity name cannot be empty");

        if (entity.Length > 128)
            throw new ArgumentException($"Entity name too long (max 128 characters): {entity}");

        // Check for invalid characters
        if (entity.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            throw new ArgumentException($"Entity name contains invalid characters (use only letters, numbers, and underscores): {entity}");

        // Entity names should not start with a number
        if (char.IsDigit(entity[0]))
            throw new ArgumentException($"Entity name cannot start with a number: {entity}");
    }

    /// <summary>
    /// Validate GUID format
    /// </summary>
    public static Guid ValidateGuid(string id, string parameterName = "id")
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException($"{parameterName} cannot be empty");

        if (!Guid.TryParse(id, out var guid))
            throw new ArgumentException($"Invalid GUID format for {parameterName}: {id}. Expected format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx");

        if (guid == Guid.Empty)
            throw new ArgumentException($"{parameterName} cannot be empty GUID (00000000-0000-0000-0000-000000000000)");

        return guid;
    }

    /// <summary>
    /// Validate field name format
    /// </summary>
    public static void ValidateFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name cannot be empty");

        if (fieldName.Length > 128)
            throw new ArgumentException($"Field name too long (max 128 characters): {fieldName}");

        // Check for invalid characters
        if (fieldName.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            throw new ArgumentException($"Field name contains invalid characters (use only letters, numbers, and underscores): {fieldName}");
    }

    /// <summary>
    /// Validate field names array
    /// </summary>
    public static void ValidateFieldNames(string[]? fieldNames)
    {
        if (fieldNames == null || fieldNames.Length == 0)
            return;

        if (fieldNames.Length > 50)
            throw new ArgumentException($"Too many fields selected (max 50): {fieldNames.Length}");

        foreach (var field in fieldNames)
        {
            ValidateFieldName(field);
        }
    }

    /// <summary>
    /// Validate OData filter expression for potentially dangerous operations
    /// </summary>
    public static void ValidateFilterExpression(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return;

        if (filter.Length > 1000)
            throw new ArgumentException($"Filter expression too long (max 1000 characters): {filter.Length}");

        // Check for potentially dangerous patterns (basic security check)
        var dangerousPatterns = new[] { "--", "/*", "*/", "xp_", "sp_", "exec(", "execute(" };
        foreach (var pattern in dangerousPatterns)
        {
            if (filter.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Filter expression contains disallowed pattern: {pattern}");
        }
    }

    /// <summary>
    /// Validate page size for list operations
    /// </summary>
    public static int ValidatePageSize(int pageSize)
    {
        if (pageSize < 1)
            throw new ArgumentException($"Page size must be at least 1, got: {pageSize}");

        if (pageSize > 250)
            throw new ArgumentException($"Page size exceeds maximum of 250, got: {pageSize}");

        return pageSize;
    }

    /// <summary>
    /// Validate top count for query operations
    /// </summary>
    public static int ValidateTopCount(int? top)
    {
        if (!top.HasValue)
            return 50; // Default

        if (top.Value < 1)
            throw new ArgumentException($"Top count must be at least 1, got: {top.Value}");

        if (top.Value > 5000)
            throw new ArgumentException($"Top count exceeds maximum of 5000, got: {top.Value}");

        return top.Value;
    }
}

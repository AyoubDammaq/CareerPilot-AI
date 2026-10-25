using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CareerPilot.Infrastructure.Persistence;

public sealed class UtcDateTimeOffsetConverter()
    : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v);

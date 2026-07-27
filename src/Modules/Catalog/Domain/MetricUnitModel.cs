namespace IUMP.Modules.Catalog.Domain;

public readonly record struct MetricId(Guid Value)
{
    public static MetricId New() => new(Guid.NewGuid());
    public static readonly MetricId ElectricPower = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    public static readonly MetricId ElectricalEnergy = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    public override string ToString() => Value.ToString("D");
}

public enum MetricStatus { Active, Inactive }

public sealed class Metric
{
    public MetricId Id { get; }
    public string Code { get; }
    public string Name { get; }
    public MetricStatus Status { get; private set; }
    public long Version { get; private set; }

    public Metric(MetricId id, string code, string name, MetricStatus status, long version)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Metric ID is required.", nameof(id));
        Code = NormalizeCode(code, nameof(code), 50);
        Name = RequireText(name, nameof(name), 200);
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        Id = id;
        Status = status;
        Version = version;
    }

    public bool Activate()
    {
        if (Status == MetricStatus.Active) return false;
        Status = MetricStatus.Active;
        Version++;
        return true;
    }

    public bool Inactivate()
    {
        if (Status == MetricStatus.Inactive) return false;
        Status = MetricStatus.Inactive;
        Version++;
        return true;
    }

    public bool IsActive() => Status == MetricStatus.Active;

    internal static string NormalizeCode(string value, string paramName, int maxLength)
    {
        var normalized = RequireText(value, paramName, maxLength).ToUpperInvariant();
        return normalized;
    }

    internal static string RequireText(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", paramName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(paramName);
        return normalized;
    }
}

public readonly record struct UnitId(Guid Value)
{
    public static UnitId New() => new(Guid.NewGuid());
    public static readonly UnitId Kilowatt = new(Guid.Parse("00000000-0000-0000-0000-000000000011"));
    public static readonly UnitId KilowattHour = new(Guid.Parse("00000000-0000-0000-0000-000000000012"));
    public override string ToString() => Value.ToString("D");
}

public enum MetricUnitStatus { Active, Inactive }

public sealed class MetricUnit
{
    public UnitId Id { get; }
    public string Code { get; }
    public string Symbol { get; }
    public MetricUnitStatus Status { get; private set; }
    public long Version { get; private set; }

    public MetricUnit(UnitId id, string code, string symbol, MetricUnitStatus status, long version)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Unit ID is required.", nameof(id));
        Code = Metric.NormalizeCode(code, nameof(code), 50);
        Symbol = Metric.RequireText(symbol, nameof(symbol), 50);
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        Id = id;
        Status = status;
        Version = version;
    }

    public bool Activate()
    {
        if (Status == MetricUnitStatus.Active) return false;
        Status = MetricUnitStatus.Active;
        Version++;
        return true;
    }

    public bool Inactivate()
    {
        if (Status == MetricUnitStatus.Inactive) return false;
        Status = MetricUnitStatus.Inactive;
        Version++;
        return true;
    }

    public bool IsActive() => Status == MetricUnitStatus.Active;
}

public sealed class MetricUnitCompatibility
{
    public MetricId MetricId { get; }
    public UnitId UnitId { get; }
    public bool IsCanonical { get; private set; }
    public long Version { get; private set; }

    public MetricUnitCompatibility(MetricId metricId, UnitId unitId, bool isCanonical, long version)
    {
        if (metricId.Value == Guid.Empty) throw new ArgumentException("Metric ID is required.", nameof(metricId));
        if (unitId.Value == Guid.Empty) throw new ArgumentException("Unit ID is required.", nameof(unitId));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        MetricId = metricId;
        UnitId = unitId;
        IsCanonical = isCanonical;
        Version = version;
    }

    public bool SetCanonical(bool value)
    {
        if (IsCanonical == value) return false;
        IsCanonical = value;
        Version++;
        return true;
    }
}

public sealed record CatalogSeedDefinition(
    MetricId MetricId,
    string MetricCode,
    string MetricName,
    UnitId UnitId,
    string UnitCode,
    string UnitSymbol);

public static class CatalogSeedDefinitions
{
    public static IReadOnlyList<CatalogSeedDefinition> All { get; } = new[]
    {
        new CatalogSeedDefinition(MetricId.ElectricPower, "ELECTRIC_POWER", "Electric Power", UnitId.Kilowatt, "KW", "kW"),
        new CatalogSeedDefinition(MetricId.ElectricalEnergy, "ELECTRICAL_ENERGY", "Electrical Energy", UnitId.KilowattHour, "KWH", "kWh")
    };
}

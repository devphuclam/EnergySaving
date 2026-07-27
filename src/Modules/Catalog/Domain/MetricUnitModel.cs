namespace IUMP.Modules.Catalog.Domain;

public readonly record struct MetricId(Guid Value)
{
    public static MetricId New() => new(Guid.NewGuid());
    public static readonly MetricId ElectricPower = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    public static readonly MetricId ElectricalEnergy = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    public override string ToString() => Value.ToString("D");
}

public sealed class Metric
{
    public MetricId Id { get; }
    public string Code { get; }
    public string Name { get; }
    public MetricStatus Status { get; private set; }
    public long Version { get; private set; }

    public Metric(MetricId id, string code, string name, MetricStatus status, long version)
    {
        Id = id;
        Code = code.ToUpperInvariant();
        Name = name;
        Status = status;
        Version = version;
    }

    public void Activate() { if (Status == MetricStatus.Inactive) { Status = MetricStatus.Active; Version++; } }
    public void Inactivate() { if (Status == MetricStatus.Active) { Status = MetricStatus.Inactive; Version++; } }
    public bool IsActive() => Status == MetricStatus.Active;
}

public enum MetricStatus { Active, Inactive }

public readonly record struct UnitId(Guid Value)
{
    public static UnitId New() => new(Guid.NewGuid());
    public static readonly UnitId Kilowatt = new(Guid.Parse("00000000-0000-0000-0000-000000000011"));
    public static readonly UnitId KilowattHour = new(Guid.Parse("00000000-0000-0000-0000-000000000012"));
    public override string ToString() => Value.ToString("D");
}

public sealed class MetricUnit
{
    public UnitId Id { get; }
    public string Code { get; }
    public string Symbol { get; }
    public MetricUnitStatus Status { get; private set; }
    public long Version { get; private set; }

    public MetricUnit(UnitId id, string code, string symbol, MetricUnitStatus status, long version)
    {
        Id = id;
        Code = code.ToUpperInvariant();
        Symbol = symbol;
        Status = status;
        Version = version;
    }

    public void Activate() { if (Status == MetricUnitStatus.Inactive) { Status = MetricUnitStatus.Active; Version++; } }
    public void Inactivate() { if (Status == MetricUnitStatus.Active) { Status = MetricUnitStatus.Inactive; Version++; } }
    public bool IsActive() => Status == MetricUnitStatus.Active;
}

public enum MetricUnitStatus { Active, Inactive }

public sealed class MetricUnitCompatibility
{
    public MetricId MetricId { get; }
    public UnitId UnitId { get; }
    public bool IsCanonical { get; private set; }
    public long Version { get; private set; }

    public MetricUnitCompatibility(MetricId metricId, UnitId unitId, bool isCanonical, long version)
    {
        MetricId = metricId;
        UnitId = unitId;
        IsCanonical = isCanonical;
        Version = version;
    }

    public void SetCanonical(bool value) { IsCanonical = value; Version++; }
}

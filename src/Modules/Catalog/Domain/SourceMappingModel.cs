namespace IUMP.Modules.Catalog.Domain;

public readonly record struct DataSourceId(Guid Value)
{
    public static DataSourceId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public enum SourceType { Simulator }
public enum SourceStatus { Draft, Active, Suspended, Decommissioned }

public sealed class DataSource
{
    public DataSourceId Id { get; }
    public string Code { get; }
    public string Name { get; }
    public SourceType SourceType { get; }
    public SourceStatus Status { get; private set; }
    public long Version { get; private set; }

    public DataSource(DataSourceId id, string code, string name, SourceType sourceType, SourceStatus status, long version)
    {
        Id = id;
        Code = code.ToUpperInvariant();
        Name = name;
        SourceType = sourceType;
        Status = status;
        Version = version;
    }

    public bool TryTransitionTo(SourceStatus target)
    {
        var allowed = (Status, target) switch
        {
            (SourceStatus.Draft, SourceStatus.Active) => true,
            (SourceStatus.Draft, SourceStatus.Decommissioned) => true,
            (SourceStatus.Active, SourceStatus.Suspended) => true,
            (SourceStatus.Active, SourceStatus.Decommissioned) => true,
            (SourceStatus.Suspended, SourceStatus.Active) => true,
            (SourceStatus.Suspended, SourceStatus.Decommissioned) => true,
            _ => false
        };
        if (!allowed) return false;
        Status = target;
        Version++;
        return true;
    }

    public bool IsDecommissioned => Status == SourceStatus.Decommissioned;
}

public readonly record struct MappingId(Guid Value)
{
    public static MappingId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public enum MappingStatus { Draft, Active, Inactive, Superseded }

public sealed class SourcePointMapping
{
    public MappingId Id { get; }
    public DataSourceId DataSourceId { get; }
    public string PointId { get; }
    public MappingStatus Status { get; private set; }
    public DateTime EffectiveFrom { get; }
    public DateTime? EffectiveTo { get; private set; }
    public long Version { get; private set; }

    public SourcePointMapping(MappingId id, DataSourceId dataSourceId, string pointId,
        MappingStatus status, DateTime effectiveFrom, DateTime? effectiveTo, long version)
    {
        Id = id;
        DataSourceId = dataSourceId;
        PointId = pointId;
        Status = status;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Version = version;
    }

    public bool TryActivate() { if (Status == MappingStatus.Draft) { Status = MappingStatus.Active; Version++; return true; } return false; }
    public bool TryInactivate() { if (Status == MappingStatus.Active) { Status = MappingStatus.Inactive; Version++; return true; } return false; }
    public bool TrySupersede() { if (Status is MappingStatus.Active or MappingStatus.Inactive) { Status = MappingStatus.Superseded; Version++; return true; } return false; }
    public bool IsSuperseded => Status == MappingStatus.Superseded;
    public bool IsActive => Status == MappingStatus.Active;

    public bool OverlapsWith(SourcePointMapping other)
    {
        if (other.PointId != PointId) return false;
        if (other.Id == Id) return false;
        var aStart = EffectiveFrom;
        var aEnd = EffectiveTo ?? DateTime.MaxValue;
        var bStart = other.EffectiveFrom;
        var bEnd = other.EffectiveTo ?? DateTime.MaxValue;
        return aStart < bEnd && bStart < aEnd;
    }
}

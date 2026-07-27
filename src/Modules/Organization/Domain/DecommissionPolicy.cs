namespace IUMP.Modules.Organization.Domain;

public sealed record DecommissionDecision(bool IsAllowed, string Code)
{
    public static DecommissionDecision Allowed() => new(true, string.Empty);
    public static DecommissionDecision Denied(string code) => new(false, code);
}

public static class DecommissionPolicy
{
    public static DecommissionDecision EvaluateAsset(Asset asset, IReadOnlyList<MeasurementPoint> childPoints)
    {
        if (asset.IsDecommissioned) return DecommissionDecision.Denied("INVALID_STATE");
        if (asset.Status is not (AssetStatus.Active or AssetStatus.Inactive))
            return DecommissionDecision.Denied("INVALID_STATE");
        return childPoints.Any(p => p.IsActive)
            ? DecommissionDecision.Denied("ACTIVE_CHILD_POINT")
            : DecommissionDecision.Allowed();
    }

    public static DecommissionDecision EvaluatePoint(MeasurementPoint point, bool hasRunningSimulator)
    {
        if (point.IsDecommissioned || point.Status is not (PointStatus.Active or PointStatus.Inactive))
            return DecommissionDecision.Denied("INVALID_STATE");
        return hasRunningSimulator
            ? DecommissionDecision.Denied("RUNNING_SIMULATOR")
            : DecommissionDecision.Allowed();
    }

    // Compatibility helpers remain pure and do not replace the guarded command flow.
    public static bool CanDecommissionAsset(Asset asset, IReadOnlyList<MeasurementPoint> childPoints) =>
        EvaluateAsset(asset, childPoints).IsAllowed;

    public static bool CanDecommissionPoint(MeasurementPoint point, bool hasRunningSimulator) =>
        EvaluatePoint(point, hasRunningSimulator).IsAllowed;
}

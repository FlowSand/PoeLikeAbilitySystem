namespace Combat.Editor.GraphAuthoring
{
    /// <summary>
    /// Sentinel types for NGP port type discrimination and coloring.
    /// These types are never instantiated - they exist solely for System.Type identity.
    /// </summary>

    public struct PortTypeNumber { }
    public struct PortTypeBool { }
    public struct PortTypeEntityId { }
    public struct PortTypeEntityList { }
    public struct PortTypeDamageSpec { }
}

public struct ItemDataQuery
{
    public bool inclPosition;
    public bool inclRotation;
    public bool inclBounds;
    public bool inclNeighbors;
    public bool inclDescription;
    public bool inclReachable;

    public ItemDataQuery(bool includePosition, bool includeRotation, bool includeBounds,
                         bool includeNeighbors, bool includeDescription, bool includeReachable)
    {
        inclPosition = includePosition;
        inclRotation = includeRotation;
        inclBounds = includeBounds;
        inclNeighbors = includeNeighbors;
        inclDescription = includeDescription;
        inclReachable = includeReachable;
    }
}

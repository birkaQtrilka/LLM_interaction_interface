using System;

[Serializable]
public struct ContextQuery
{
    public bool getSpots;
    public bool getObjects;
    public ObjectFlags objectFlags;
    public UserFlags userFlags;

    // TODO: getAnimations, getChatHistory, getAgent
    public static ContextQuery GetFullContext()
    {
        return new ContextQuery
        {
            getSpots = true,
            getObjects = true,
            userFlags = new UserFlags
            (
                position: true,
                rotation: true,
                neighbours: true
            ),
            objectFlags = new ObjectFlags
            (
                position: true,
                rotation: true,
                description: true,
                bounds: true,
                neighbours: true
            )
        };
    }
}

[Serializable]
public struct UserFlags
{
    public bool position;
    public bool rotation;
    public bool neighbours;

    public UserFlags(bool position, bool rotation, bool neighbours)
    {
        this.position = position;
        this.rotation = rotation;
        this.neighbours = neighbours;
    }
}

[Serializable]
public struct ObjectFlags
{
    public bool position;
    public bool rotation;
    public bool description;
    public bool bounds;
    public bool neighbours;

    public ObjectFlags(bool position, bool rotation, bool description, bool bounds, bool neighbours)
    {
        this.position = position;
        this.rotation = rotation;
        this.description = description;
        this.bounds = bounds;
        this.neighbours = neighbours;
    }
}

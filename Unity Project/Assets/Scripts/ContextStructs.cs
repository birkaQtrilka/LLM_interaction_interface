using System;

[Serializable]
public class ContextQuery
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
            {
                position = true,
                rotation = true,
                neighbours = true
            },
            objectFlags = new ObjectFlags
            {
                position = true,
                rotation = true,
                description = true,
                neighbours = true
            }
        };
    }
}

[Serializable]
public class UserFlags
{
    public bool position;
    public bool rotation;
    public bool neighbours;
}

[Serializable]
public class ObjectFlags
{
    public bool position;
    public bool rotation;
    public bool description;
    public bool neighbours;
}

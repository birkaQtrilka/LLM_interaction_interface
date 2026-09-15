using System;

[Serializable]
public class ContextQuery
{
    public bool getSpots;
    public UserFlags getUser;
    public ObjectFlags getObjects;

    public static ContextQuery GetFullContext()
    {
        return new ContextQuery
        {
            getSpots = true,
            getUser = new UserFlags
            {
                position = true,
                rotation = true,
                neighbours = true
            },
            getObjects = new ObjectFlags
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

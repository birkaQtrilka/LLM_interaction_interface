using System;

[Serializable]
public class ContextResponse
{
    public bool getSpots;
    public UserContext getUser;
    public ObjectContext getObjects;

    public static ContextResponse GetFullContext()
    {
        return new ContextResponse
        {
            getSpots = true,
            getUser = new UserContext
            {
                position = true,
                rotation = true,
                neighbours = true
            },
            getObjects = new ObjectContext
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
public class UserContext
{
    public bool position;
    public bool rotation;
    public bool neighbours;
}

[Serializable]
public class ObjectContext
{
    public bool position;
    public bool rotation;
    public bool description;
    public bool neighbours;
}

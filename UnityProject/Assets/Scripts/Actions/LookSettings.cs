
[System.Serializable]
public struct LookSettings
{
    public float MaxSightDistance;
    public float CheckInterval;
    /// <summary>
    /// When the ray travels toward that point, it often hits the item’s own collider a little before reaching the exact point, 
    /// because the collider has some size around its pivot. If you required “nothing was hit before the target”, 
    /// the NPC would be blocked by the very item it is trying to look at, and would never see it.
    /// </summary>
    public float ItemTolerance;

    public LookSettings(float maxSightDistance, float checkInterval, float itemTolerance)
    {
        MaxSightDistance = maxSightDistance;
        CheckInterval = checkInterval;
        ItemTolerance = itemTolerance;
    }
}
using UnityEngine;

public static class EventLock
{
    private static string _owner;
    public static bool IsLocked => !string.IsNullOrEmpty(_owner);
    public static string Owner => _owner;

    public static bool TryAcquire(string owner)
    {
        if (IsLocked) return false;
        _owner = owner;
        return true;
    }

    public static void Release(string owner)
    {
        if (_owner == owner) _owner = null;
    }
}
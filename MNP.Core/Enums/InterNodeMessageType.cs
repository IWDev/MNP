
namespace MNP.Core.Enums
{
    public enum InterNodeMessageType
    {
        None = 0,
        AddToCache = 1,
        AddToQueue = 2,
        RemoveFromCache = 3,
        RemoveFromQueue = 4,
        ChangeMessageStateInQueue = 5,
        NewNodeDiscovered = 6,
        FullCacheUpdateSent = 7,
        FullCacheUpdateReceived = 8,
        FullQueueUpdateSent = 9,
        FullQueueUpdateReceived = 10,
        StartUp = 11
    }
}

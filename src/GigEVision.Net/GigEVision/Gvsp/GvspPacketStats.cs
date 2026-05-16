namespace GenICam.Net.GigEVision.Gvsp;

public sealed record GvspPacketStats(
    int ReceivedPacketCount,
    int LeaderCount,
    int PayloadCount,
    int TrailerCount,
    int OtherCount)
{
    public string Summary => $"L:{LeaderCount} P:{PayloadCount} T:{TrailerCount} O:{OtherCount}";
}

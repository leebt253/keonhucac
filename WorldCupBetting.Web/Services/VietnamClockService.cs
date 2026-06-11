namespace WorldCupBetting.Web.Services;

public class VietnamClockService : IClockService
{
    private static readonly TimeSpan Utc7 = TimeSpan.FromHours(7);

    public DateTime VietnamNow() => DateTime.UtcNow.Add(Utc7);

    public bool IsLocked(DateTime matchTime) => VietnamNow() >= matchTime;
}

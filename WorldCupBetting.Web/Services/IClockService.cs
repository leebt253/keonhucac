namespace WorldCupBetting.Web.Services;

public interface IClockService
{
    DateTime VietnamNow();
    bool IsLocked(DateTime matchTime);
}

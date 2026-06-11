namespace WorldCupBetting.Web.Services;

public static class ResultParser
{
    public static bool TryParseScore(string result, out int goalsA, out int goalsB)
    {
        goalsA = 0;
        goalsB = 0;

        if (string.IsNullOrWhiteSpace(result))
        {
            return false;
        }

        var parts = result.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        return int.TryParse(parts[0], out goalsA) && int.TryParse(parts[1], out goalsB);
    }
}

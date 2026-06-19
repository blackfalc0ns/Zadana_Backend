namespace Zadana.Application.Common.Models;

public static class OffsetLimitPagination
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public static int NormalizeOffset(int offset) => offset < 0 ? 0 : offset;

    public static int NormalizeLimit(int limit)
    {
        if (limit <= 0)
        {
            return DefaultLimit;
        }

        return Math.Min(limit, MaxLimit);
    }

    public static bool HasMore(int offset, int limit, int total) => offset + limit < total;
}

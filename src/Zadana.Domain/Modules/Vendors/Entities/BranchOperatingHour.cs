namespace Zadana.Domain.Modules.Vendors.Entities;

public class BranchOperatingHour
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid BranchId { get; private set; }
    public int DayOfWeek { get; private set; }
    public TimeSpan OpenTime { get; private set; }
    public TimeSpan CloseTime { get; private set; }
    public bool IsClosed { get; private set; }

    // Navigation
    public VendorBranch Branch { get; private set; } = null!;

    private BranchOperatingHour() { }

    public BranchOperatingHour(Guid branchId, int dayOfWeek, TimeSpan openTime, TimeSpan closeTime, bool isClosed = false)
    {
        if (dayOfWeek < 0 || dayOfWeek > 6)
            throw new ArgumentOutOfRangeException(nameof(dayOfWeek), "Must be 0 (Sunday) through 6 (Saturday).");

        BranchId = branchId;
        DayOfWeek = dayOfWeek;
        OpenTime = openTime;
        CloseTime = closeTime;
        IsClosed = isClosed;
    }

    public void Update(TimeSpan openTime, TimeSpan closeTime, bool isClosed)
    {
        OpenTime = openTime;
        CloseTime = closeTime;
        IsClosed = isClosed;
    }
}

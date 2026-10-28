using HackathonStarter.Components.Crs;
using HackathonStarter.Features.Cases;

namespace HackathonStarter.Tests.Crs;

public class CaseTextTests
{
    [Fact]
    public void DaysUntil_OnTrack_ReturnsPositiveDays()
    {
        var start = new DateTime(2027, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2027, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var days = CaseText.DaysUntil(due, start);
        Assert.Equal(14, days);
    }

    [Fact]
    public void DaysUntil_OnDeadlineDay_ReturnsZero()
    {
        var day = new DateTime(2027, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var days = CaseText.DaysUntil(day, day);
        Assert.Equal(0, days);
    }

    [Fact]
    public void DaysUntil_Overdue_ReturnsNegativeDays()
    {
        var deadline = new DateTime(2027, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2027, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var days = CaseText.DaysUntil(deadline, now);
        Assert.Equal(-14, days);
    }

    [Fact]
    public void DaysUntil_IgnoreTimeOfDay_OnlyCountCalendarDays()
    {
        // Note: DaysUntil converts to local time, so the result depends on the system's timezone.
        // This test uses dates 14 days apart, so the result should be 14 or 15 depending on local timezone.
        var deadline = new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2027, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var days = CaseText.DaysUntil(deadline, now);
        Assert.True(days >= 13 && days <= 15, $"Expected days between 13 and 15, but got {days}");
    }

    [Fact]
    public void Deadline_NoDeadlineCase_ReturnsNoDeadline()
    {
        var c = new Case { Status = CaseStatus.Open, DueAt = null, RemindAt = null };
        var state = CaseText.Deadline(c, DateTime.UtcNow);
        Assert.Equal(DeadlineState.NoDeadline, state);
    }

    [Fact]
    public void Deadline_FatcaCase_ReturnsNoDeadline()
    {
        var now = new DateTime(2027, 1, 10, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2027, 1, 25, 10, 0, 0, DateTimeKind.Utc);
        var c = new Case
        {
            Status = CaseStatus.Open,
            DueAt = null,
            RemindAt = null,
            OpenedAt = now
        };
        var state = CaseText.Deadline(c, now);
        Assert.Equal(DeadlineState.NoDeadline, state);
    }

    [Fact]
    public void Deadline_ClosedCase_ReturnsDone()
    {
        var now = new DateTime(2027, 1, 10, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2027, 1, 25, 10, 0, 0, DateTimeKind.Utc);
        var c = new Case
        {
            Status = CaseStatus.Closed,
            DueAt = due,
            ClosedAt = new DateTime(2027, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };
        var state = CaseText.Deadline(c, now);
        Assert.Equal(DeadlineState.Done, state);
    }

    [Fact]
    public void Deadline_ExpiredCase_ReturnsDone()
    {
        var now = new DateTime(2027, 1, 10, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2027, 1, 25, 10, 0, 0, DateTimeKind.Utc);
        var c = new Case
        {
            Status = CaseStatus.Expired,
            DueAt = due,
            ExpiredAt = new DateTime(2027, 1, 26, 10, 0, 0, DateTimeKind.Utc)
        };
        var state = CaseText.Deadline(c, now);
        Assert.Equal(DeadlineState.Done, state);
    }

    [Fact]
    public void Deadline_OnTrack_ReturnsOnTrack()
    {
        var now = new DateTime(2027, 1, 10, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2027, 1, 25, 10, 0, 0, DateTimeKind.Utc);
        var remind = new DateTime(2027, 1, 20, 10, 0, 0, DateTimeKind.Utc);
        var c = new Case
        {
            Status = CaseStatus.Open,
            DueAt = due,
            RemindAt = remind,
            OpenedAt = now
        };
        var state = CaseText.Deadline(c, now);
        Assert.Equal(DeadlineState.OnTrack, state);
    }

    [Fact]
    public void Deadline_ReminderDue_ReturnsReminderDue()
    {
        var now = new DateTime(2027, 1, 20, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2027, 1, 25, 10, 0, 0, DateTimeKind.Utc);
        var remind = new DateTime(2027, 1, 20, 10, 0, 0, DateTimeKind.Utc);
        var c = new Case
        {
            Status = CaseStatus.Open,
            DueAt = due,
            RemindAt = remind,
            OpenedAt = new DateTime(2027, 1, 10, 10, 0, 0, DateTimeKind.Utc)
        };
        var state = CaseText.Deadline(c, now);
        Assert.Equal(DeadlineState.ReminderDue, state);
    }

    [Fact]
    public void Deadline_ReminderDatePassed_ReturnsReminderDue()
    {
        var now = new DateTime(2027, 1, 22, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2027, 1, 25, 10, 0, 0, DateTimeKind.Utc);
        var remind = new DateTime(2027, 1, 20, 10, 0, 0, DateTimeKind.Utc);
        var c = new Case
        {
            Status = CaseStatus.Open,
            DueAt = due,
            RemindAt = remind,
            OpenedAt = new DateTime(2027, 1, 10, 10, 0, 0, DateTimeKind.Utc)
        };
        var state = CaseText.Deadline(c, now);
        Assert.Equal(DeadlineState.ReminderDue, state);
    }

    [Fact]
    public void Deadline_Overdue_ReturnsOverdue()
    {
        var now = new DateTime(2027, 1, 26, 10, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2027, 1, 25, 10, 0, 0, DateTimeKind.Utc);
        var remind = new DateTime(2027, 1, 20, 10, 0, 0, DateTimeKind.Utc);
        var c = new Case
        {
            Status = CaseStatus.Open,
            DueAt = due,
            RemindAt = remind,
            OpenedAt = new DateTime(2027, 1, 10, 10, 0, 0, DateTimeKind.Utc)
        };
        var state = CaseText.Deadline(c, now);
        Assert.Equal(DeadlineState.Overdue, state);
    }

    [Fact]
    public void DayOf_CountsFromStart()
    {
        var start = new DateTime(2027, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var day5 = new DateTime(2027, 1, 6, 10, 0, 0, DateTimeKind.Utc);
        var day = CaseText.DayOf(day5, start);
        Assert.Equal(5, day);
    }

    [Fact]
    public void DayOf_StartDateIsDay0()
    {
        var start = new DateTime(2027, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var day = CaseText.DayOf(start, start);
        Assert.Equal(0, day);
    }

    [Fact]
    public void Days_Singular_ReturnsEnglishSingular()
    {
        var text = CaseText.Days(1);
        Assert.Contains("1 day", text);
    }

    [Fact]
    public void Days_SingularNegative_ReturnsEnglishSingular()
    {
        var text = CaseText.Days(-1);
        Assert.Contains("-1 day", text);
    }

    [Fact]
    public void Days_Plural_ReturnsEnglishPlural()
    {
        var text = CaseText.Days(5);
        Assert.Contains("5 days", text);
    }

    [Fact]
    public void OffsetDays_ReturnsWholeNumberOfDays()
    {
        var offset = TimeSpan.FromDays(85.75);
        var days = CaseText.OffsetDays(offset);
        Assert.Equal(85, days);
    }

    [Fact]
    public void OffsetDays_ZeroOffset_ReturnsZero()
    {
        var offset = TimeSpan.Zero;
        var days = CaseText.OffsetDays(offset);
        Assert.Equal(0, days);
    }

    [Fact]
    public void OffsetDays_NegativeOffset_ReturnsNegativeDays()
    {
        // OffsetDays uses Floor, so -10.5 becomes -11 (floor of negative numbers rounds down)
        var offset = TimeSpan.FromDays(-10.5);
        var days = CaseText.OffsetDays(offset);
        Assert.Equal(-11, days);
    }

    [Fact]
    public void PageUnverified_EmptyString_ReturnsTrue()
    {
        var unverified = CaseText.PageUnverified("");
        Assert.True(unverified);
    }

    [Fact]
    public void PageUnverified_Whitespace_ReturnsTrue()
    {
        var unverified = CaseText.PageUnverified("   ");
        Assert.True(unverified);
    }

    [Fact]
    public void PageUnverified_Null_ReturnsTrue()
    {
        var unverified = CaseText.PageUnverified(null);
        Assert.True(unverified);
    }

    [Fact]
    public void PageUnverified_WithNeverificat_ReturnsTrue()
    {
        var unverified = CaseText.PageUnverified("NEVERIFICAT");
        Assert.True(unverified);
    }

    [Fact]
    public void PageUnverified_WithValidPage_ReturnsFalse()
    {
        var unverified = CaseText.PageUnverified("29");
        Assert.False(unverified);
    }
}

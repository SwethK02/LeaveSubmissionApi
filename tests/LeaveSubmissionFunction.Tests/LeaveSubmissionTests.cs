using FluentAssertions;
using LeaveSubmissionFunction.Models;
using LeaveSubmissionFunction.Services;
using LeaveSubmissionFunction.Validators;
using Moq;
using LeaveSubmissionFunction.Data;
using Xunit;

namespace LeaveSubmissionFunction.Tests;

public class LeaveSubmissionValidatorTests
{
    private readonly LeaveSubmissionValidator _validator = new();

    private static LeaveSubmissionPayload ValidPayload() => new()
    {
        SubmissionId = "LS-2026-000123",
        SubmittedDate = "2026-02-15",
        Status = "Submitted",
        Worker = new WorkerInfo { WorkerId = "W123456" },
        LeavePeriod = new LeavePeriod
        {
            StartDate = "2026-03-02",
            EndDate = "2026-03-06",   // 5 weekdays
            TotalWorkingDays = 5
        },
        LeaveDetails = new List<LeaveDetail>
        {
            new() { LeaveTypeCode = "AL", Quantity = 5 }
        }
    };

    [Fact]
    public async Task ValidPayload_PassesValidation()
    {
        var result = await _validator.ValidateAsync(ValidPayload());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task MissingSubmissionId_FailsValidation()
    {
        var payload = ValidPayload();
        payload.SubmissionId = "";
        var result = await _validator.ValidateAsync(payload);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SubmissionId");
    }

    [Fact]
    public async Task StartDateAfterEndDate_FailsValidation()
    {
        var payload = ValidPayload();
        payload.LeavePeriod.StartDate = "2026-03-10";
        payload.LeavePeriod.EndDate = "2026-03-06";
        var result = await _validator.ValidateAsync(payload);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("StartDate"));
    }

    [Fact]
    public async Task IncorrectWorkingDayCount_FailsValidation()
    {
        var payload = ValidPayload();
        payload.LeavePeriod.TotalWorkingDays = 99; // wrong
        var result = await _validator.ValidateAsync(payload);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("TotalWorkingDays"));
    }

    [Fact]
    public async Task MissingLeaveDetails_FailsValidation()
    {
        var payload = ValidPayload();
        payload.LeaveDetails = new List<LeaveDetail>();
        var result = await _validator.ValidateAsync(payload);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ZeroQuantity_FailsValidation()
    {
        var payload = ValidPayload();
        payload.LeaveDetails[0].Quantity = 0;
        var result = await _validator.ValidateAsync(payload);
        result.IsValid.Should().BeFalse();
    }
}

public class LeaveSubmissionServiceTests
{
    private readonly Mock<ILeaveRepository> _repoMock = new();

    private LeaveSubmissionService CreateService() =>
        new(_repoMock.Object);

    private static LeaveSubmissionPayload ValidPayload() => new()
    {
        SubmissionId = "LS-2026-000123",
        SubmittedDate = "2026-02-15",
        Status = "Submitted",
        Worker = new WorkerInfo { WorkerId = "W123456" },
        LeavePeriod = new LeavePeriod
        {
            StartDate = "2026-03-02 00:00:00",
            EndDate = "2026-03-20 23:59:59",
            TotalWorkingDays = 15
        },
        LeaveDetails = new List<LeaveDetail>
        {
            new() { LeaveTypeCode = "AL", LeaveCategory = "Paid", UnitOfMeasure = "Days", Quantity = 15 }
        }
    };

    [Fact]
    public async Task ProcessAsync_PersistsCorrectNumberOfWorkingDays()
    {
        _repoMock.Setup(r => r.SubmissionExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.SaveLeaveSubmissionAsync(It.IsAny<LeaveSubmissionEntity>(),
            It.IsAny<IEnumerable<LeaveDayEntity>>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.ProcessAsync(ValidPayload());

        result.WorkingDaysPersisted.Should().Be(15);
        result.SubmissionId.Should().Be("LS-2026-000123");
    }

    [Fact]
    public async Task ProcessAsync_ThrowsOnDuplicateSubmission()
    {
        _repoMock.Setup(r => r.SubmissionExistsAsync("LS-2026-000123")).ReturnsAsync(true);

        var service = CreateService();
        var act = async () => await service.ProcessAsync(ValidPayload());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task ProcessAsync_ExcludesWeekends()
    {
        // 2026-03-02 (Mon) to 2026-03-08 (Sun) = 5 working days
        var payload = ValidPayload();
        payload.LeavePeriod.StartDate = "2026-03-02";
        payload.LeavePeriod.EndDate = "2026-03-08";
        payload.LeavePeriod.TotalWorkingDays = 5;
        payload.LeaveDetails[0].Quantity = 5;

        _repoMock.Setup(r => r.SubmissionExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        IEnumerable<LeaveDayEntity>? captured = null;
        _repoMock.Setup(r => r.SaveLeaveSubmissionAsync(
            It.IsAny<LeaveSubmissionEntity>(),
            It.IsAny<IEnumerable<LeaveDayEntity>>()))
            .Callback<LeaveSubmissionEntity, IEnumerable<LeaveDayEntity>>((_, days) => captured = days)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.ProcessAsync(payload);

        captured.Should().NotBeNull();
        captured!.Should().HaveCount(5);
        captured!.Should().NotContain(d =>
            d.LeaveDate.DayOfWeek == DayOfWeek.Saturday ||
            d.LeaveDate.DayOfWeek == DayOfWeek.Sunday);
    }

    [Fact]
    public async Task ProcessAsync_SavesAllLeaveDayFields()
    {
        _repoMock.Setup(r => r.SubmissionExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        IEnumerable<LeaveDayEntity>? captured = null;
        _repoMock.Setup(r => r.SaveLeaveSubmissionAsync(
            It.IsAny<LeaveSubmissionEntity>(),
            It.IsAny<IEnumerable<LeaveDayEntity>>()))
            .Callback<LeaveSubmissionEntity, IEnumerable<LeaveDayEntity>>((_, days) => captured = days)
            .Returns(Task.CompletedTask);

        var payload = ValidPayload();
        payload.LeavePeriod.StartDate = "2026-03-02";
        payload.LeavePeriod.EndDate = "2026-03-06";
        payload.LeavePeriod.TotalWorkingDays = 5;
        payload.LeaveDetails[0].Quantity = 5;

        var service = CreateService();
        await service.ProcessAsync(payload);

        var first = captured!.First();
        first.SubmissionId.Should().Be("LS-2026-000123");
        first.WorkerId.Should().Be("W123456");
        first.LeaveTypeCode.Should().Be("AL");
        first.LeaveCategory.Should().Be("Paid");
    }
}

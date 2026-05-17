using LeaveSubmissionFunction.Data;
using LeaveSubmissionFunction.Models;

namespace LeaveSubmissionFunction.Services;

public interface ILeaveSubmissionService
{
    Task<LeaveSubmissionResponse> ProcessAsync(LeaveSubmissionPayload payload);
}

public class LeaveSubmissionService : ILeaveSubmissionService
{
    private readonly ILeaveRepository _repository;

    public LeaveSubmissionService(ILeaveRepository repository)
    {
        _repository = repository;
    }

    public async Task<LeaveSubmissionResponse> ProcessAsync(LeaveSubmissionPayload payload)
    {
        if (await _repository.SubmissionExistsAsync(payload.SubmissionId))
            throw new InvalidOperationException($"Submission '{payload.SubmissionId}' already exists.");

        var startDate = DateTime.Parse(payload.LeavePeriod.StartDate).Date;
        var endDate = DateTime.Parse(payload.LeavePeriod.EndDate).Date;

        // Build LeaveSubmission entity
        var submission = new LeaveSubmissionEntity
        {
            SubmissionId = payload.SubmissionId,
            WorkerId = payload.Worker.WorkerId,
            StartDatetime = DateTime.Parse(payload.LeavePeriod.StartDate),
            EndDatetime = DateTime.Parse(payload.LeavePeriod.EndDate),
            TotalDays = payload.LeavePeriod.TotalWorkingDays,
            Status = payload.Status,
            SubmittedDate = DateTime.Parse(payload.SubmittedDate),
        };
        var workingDays = GetWorkingDays(startDate, endDate);
        var leaveDays = new List<LeaveDayEntity>();

        foreach (var day in workingDays)
        {
            foreach (var detail in payload.LeaveDetails)
            {
                var quantityPerDay = detail.Quantity / workingDays.Count;

                leaveDays.Add(new LeaveDayEntity
                {
                    SubmissionId = payload.SubmissionId,
                    WorkerId = payload.Worker.WorkerId,
                    LeaveDate = day,
                    LeaveTypeCode = detail.LeaveTypeCode,
                    LeaveCategory = detail.LeaveCategory,
                    UnitOfMeasure = detail.UnitOfMeasure,
                    Quantity = Math.Round(quantityPerDay, 2),
                });
            }
        }

        await _repository.SaveLeaveSubmissionAsync(submission, leaveDays);

        return new LeaveSubmissionResponse
        {
            SubmissionId = payload.SubmissionId,
            Status = payload.Status,
            WorkingDaysPersisted = workingDays.Count,
            Message = $"Leave submission processed successfully. {workingDays.Count} working day(s) persisted."
        };
    }

    // Returns all weekdays (Mon–Fri) between start and end dates inclusive.
    // Public holidays are out of scope per requirements.
    private static List<DateTime> GetWorkingDays(DateTime start, DateTime end)
    {
        var days = new List<DateTime>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                days.Add(d);
        }
        return days;
    }
}

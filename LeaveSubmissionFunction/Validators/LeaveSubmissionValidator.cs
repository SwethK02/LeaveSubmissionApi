using FluentValidation;
using LeaveSubmissionFunction.Models;

namespace LeaveSubmissionFunction.Validators;

public class LeaveSubmissionValidator : AbstractValidator<LeaveSubmissionPayload>
{
    public LeaveSubmissionValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("SubmissionId is required.");

        RuleFor(x => x.SubmittedDate)
            .NotEmpty().WithMessage("SubmittedDate is required.")
            .Must(BeAValidDate).WithMessage("SubmittedDate must be a valid date (yyyy-MM-dd).");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.");

        RuleFor(x => x.Worker)
            .NotNull().WithMessage("Worker is required.");

        RuleFor(x => x.Worker.WorkerId)
            .NotEmpty().WithMessage("Worker.WorkerId is required.")
            .When(x => x.Worker != null);

        RuleFor(x => x.LeavePeriod)
            .NotNull().WithMessage("LeavePeriod is required.");

        When(x => x.LeavePeriod != null, () =>
        {
            RuleFor(x => x.LeavePeriod.StartDate)
                .NotEmpty().WithMessage("LeavePeriod.StartDate is required.")
                .Must(BeAValidDate).WithMessage("LeavePeriod.StartDate must be a valid date.");

            RuleFor(x => x.LeavePeriod.EndDate)
                .NotEmpty().WithMessage("LeavePeriod.EndDate is required.")
                .Must(BeAValidDate).WithMessage("LeavePeriod.EndDate must be a valid date.");

            RuleFor(x => x)
                .Must(x => StartDateBeforeOrEqualEndDate(x.LeavePeriod.StartDate, x.LeavePeriod.EndDate))
                .WithMessage("StartDate must be less than or equal to EndDate.")
                .When(x => BeAValidDate(x.LeavePeriod.StartDate) && BeAValidDate(x.LeavePeriod.EndDate));

            RuleFor(x => x)
                .Must(x => WorkingDaysMatchPeriod(x.LeavePeriod))
                .WithMessage("TotalWorkingDays does not match the number of working days in the leave period.")
                .When(x => BeAValidDate(x.LeavePeriod.StartDate) && BeAValidDate(x.LeavePeriod.EndDate));

            RuleFor(x => x.LeavePeriod.TotalWorkingDays)
                .GreaterThan(0).WithMessage("TotalWorkingDays must be greater than 0.");
        });

        RuleFor(x => x.LeaveDetails)
            .NotEmpty().WithMessage("At least one LeaveDetail entry is required.");

        RuleForEach(x => x.LeaveDetails).ChildRules(detail =>
        {
            detail.RuleFor(d => d.LeaveTypeCode)
                .NotEmpty().WithMessage("LeaveDetail.LeaveTypeCode is required.");

            detail.RuleFor(d => d.Quantity)
                .GreaterThan(0).WithMessage("LeaveDetail.Quantity must be greater than 0.");
        });
    }

    private static bool BeAValidDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return DateTime.TryParse(value, out _);
    }

    private static bool StartDateBeforeOrEqualEndDate(string start, string end)
    {
        if (!DateTime.TryParse(start, out var startDt) || !DateTime.TryParse(end, out var endDt))
            return false;
        return startDt.Date <= endDt.Date;
    }

    private static bool WorkingDaysMatchPeriod(LeavePeriod period)
    {
        if (!DateTime.TryParse(period.StartDate, out var start) ||
            !DateTime.TryParse(period.EndDate, out var end))
            return false;

        int count = 0;
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                count++;

        return count == period.TotalWorkingDays;
    }
}

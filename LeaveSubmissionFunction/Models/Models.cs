namespace LeaveSubmissionFunction.Models;

// ─── Inbound Request ──────────────────────────────────────────────────────────

public class LeaveSubmissionRequest
{
    public LeaveSubmissionPayload LeaveSubmission { get; set; } = null!;
}

public class LeaveSubmissionPayload
{
    public string SubmissionId { get; set; } = null!;
    public string SubmittedDate { get; set; } = null!;
    public string Status { get; set; } = null!;
    public WorkerInfo Worker { get; set; } = null!;
    public LeavePeriod LeavePeriod { get; set; } = null!;
    public List<LeaveDetail> LeaveDetails { get; set; } = new();
    public ApproverInfo? Approver { get; set; }
    public string? Comments { get; set; }
}

public class WorkerInfo
{
    public string WorkerId { get; set; } = null!;
    public string? EmployeeNumber { get; set; }
    public string? SourceSystem { get; set; }
}

public class LeavePeriod
{
    public string StartDate { get; set; } = null!;
    public string EndDate { get; set; } = null!;
    public int? TotalWeeks { get; set; }
    public int TotalWorkingDays { get; set; }
}

public class LeaveDetail
{
    public string LeaveTypeCode { get; set; } = null!;
    public string? LeaveTypeDescription { get; set; }
    public string? LeaveCategory { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal Quantity { get; set; }
}

public class ApproverInfo
{
    public string? ApproverId { get; set; }
    public string? ApprovalStatus { get; set; }
}

// ─── Outbound Response ────────────────────────────────────────────────────────

public class LeaveSubmissionResponse
{
    public string SubmissionId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int WorkingDaysPersisted { get; set; }
    public string Message { get; set; } = null!;
}

public class ErrorResponse
{
    public string Error { get; set; } = null!;
    public List<string>? Details { get; set; }
}

// ─── DB Entities ──────────────────────────────────────────────────────────────

public class LeaveSubmissionEntity
{
    public string SubmissionId { get; set; } = null!;
    public string WorkerId { get; set; } = null!;
    public DateTime StartDatetime { get; set; }
    public DateTime EndDatetime { get; set; }
    public int TotalDays { get; set; }
    public string Status { get; set; } = null!;
    public DateTime SubmittedDate { get; set; }
}

public class LeaveDayEntity
{
    public string SubmissionId { get; set; } = null!;
    public string WorkerId { get; set; } = null!;
    public DateTime LeaveDate { get; set; }
    public string LeaveTypeCode { get; set; } = null!;
    public string? LeaveCategory { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal Quantity { get; set; }
}

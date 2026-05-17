using Dapper;
using LeaveSubmissionFunction.Models;
using Microsoft.Data.SqlClient;

namespace LeaveSubmissionFunction.Data;

public interface ILeaveRepository
{
    Task SaveLeaveSubmissionAsync(LeaveSubmissionEntity submission, IEnumerable<LeaveDayEntity> leaveDays);
    Task<bool> SubmissionExistsAsync(string submissionId);
}

public class LeaveRepository : ILeaveRepository
{
    private readonly string _connectionString;

    public LeaveRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> SubmissionExistsAsync(string submissionId)
    {
        const string sql = "SELECT COUNT(1) FROM LeaveSubmission WHERE SubmissionId = @SubmissionId";

        using var conn = new SqlConnection(_connectionString);
        var count = await conn.ExecuteScalarAsync<int>(sql, new { SubmissionId = submissionId });
        return count > 0;
    }

    public async Task SaveLeaveSubmissionAsync(
        LeaveSubmissionEntity submission,
        IEnumerable<LeaveDayEntity> leaveDays)
    {
        const string insertSubmission = """
            INSERT INTO LeaveSubmission
                (SubmissionId, WorkerId, StartDatetime, EndDatetime, TotalDays, Status, SubmittedDate)
            VALUES
                (@SubmissionId, @WorkerId, @StartDatetime, @EndDatetime, @TotalDays, @Status, @SubmittedDate)
            """;

        const string insertLeaveDay = """
            INSERT INTO LeaveDay
                (SubmissionId, WorkerId, LeaveDate, LeaveTypeCode, LeaveCategory, UnitOfMeasure, Quantity)
            VALUES
                (@SubmissionId, @WorkerId, @LeaveDate, @LeaveTypeCode, @LeaveCategory, @UnitOfMeasure, @Quantity)
            """;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var transaction = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(insertSubmission, submission, transaction);
            await conn.ExecuteAsync(insertLeaveDay, leaveDays, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}

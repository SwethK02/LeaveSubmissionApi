-- ============================================================
-- Leave Submission API - SQL Server Schema
-- Run this script against your Azure SQL Database
-- ============================================================

-- Create database (run separately if needed)
-- CREATE DATABASE LeaveDb;
-- GO

USE LeaveDb;
GO

-- ─── LeaveSubmission table ────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LeaveSubmission' AND xtype='U')
BEGIN
    CREATE TABLE LeaveSubmission (
        SubmissionId    VARCHAR(50)  NOT NULL,
        WorkerId        VARCHAR(50)  NOT NULL,
        StartDatetime   DATETIME     NOT NULL,
        EndDatetime     DATETIME     NOT NULL,
        TotalDays       INT          NOT NULL,
        Status          VARCHAR(20)  NOT NULL,
        SubmittedDate   DATE         NOT NULL,

        CONSTRAINT PK_LeaveSubmission PRIMARY KEY (SubmissionId)
    );
END
GO

-- ─── LeaveDay table ───────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LeaveDay' AND xtype='U')
BEGIN
    CREATE TABLE LeaveDay (
        LeaveDayId      INT          NOT NULL IDENTITY(1,1),
        SubmissionId    VARCHAR(50)  NOT NULL,
        WorkerId        VARCHAR(50)  NOT NULL,
        LeaveDate       DATE         NOT NULL,
        LeaveTypeCode   VARCHAR(10)  NOT NULL,
        LeaveCategory   VARCHAR(20)  NULL,
        UnitOfMeasure   VARCHAR(10)  NULL,
        Quantity        DECIMAL(5,2) NOT NULL,

        CONSTRAINT PK_LeaveDay PRIMARY KEY (LeaveDayId),
        CONSTRAINT FK_LeaveDay_LeaveSubmission
            FOREIGN KEY (SubmissionId) REFERENCES LeaveSubmission(SubmissionId),
        CONSTRAINT UQ_LeaveDay_SubmissionDate
            UNIQUE (SubmissionId, WorkerId, LeaveDate, LeaveTypeCode)
    );
END
GO

-- ─── Indexes ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LeaveDay_WorkerId')
    CREATE INDEX IX_LeaveDay_WorkerId ON LeaveDay (WorkerId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LeaveDay_LeaveDate')
    CREATE INDEX IX_LeaveDay_LeaveDate ON LeaveDay (LeaveDate);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LeaveSubmission_WorkerId')
    CREATE INDEX IX_LeaveSubmission_WorkerId ON LeaveSubmission (WorkerId);
GO

-- ─── Useful queries ───────────────────────────────────────────────────────────

-- View all leave days for a worker
-- SELECT * FROM LeaveDay WHERE WorkerId = 'W123456' ORDER BY LeaveDate;

-- View a full submission with its days
-- SELECT s.*, d.LeaveDate, d.LeaveTypeCode, d.Quantity
-- FROM LeaveSubmission s
-- JOIN LeaveDay d ON s.SubmissionId = d.SubmissionId
-- WHERE s.SubmissionId = 'LS-2026-000123'
-- ORDER BY d.LeaveDate;

-- Count working days per submission
-- SELECT SubmissionId, COUNT(*) AS WorkingDayCount
-- FROM LeaveDay
-- GROUP BY SubmissionId;

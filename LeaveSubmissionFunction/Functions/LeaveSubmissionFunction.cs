using System.Net;
using FluentValidation;
using LeaveSubmissionFunction.Models;
using LeaveSubmissionFunction.Services;
using LeaveSubmissionFunction.Validators;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LeaveSubmissionFunction.Functions;

public class LeaveSubmissionFunction
{
    private readonly ILeaveSubmissionService _service;
    private readonly LeaveSubmissionValidator _validator;
    private readonly ILogger<LeaveSubmissionFunction> _logger;

    public LeaveSubmissionFunction(ILeaveSubmissionService service,
        LeaveSubmissionValidator validator,
        ILogger<LeaveSubmissionFunction> logger)
    {
        _service = service;
        _validator = validator;
        _logger = logger;
    }

    [Function("SubmitLeave")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/leave-submissions")]
        HttpRequestData req)
    {
        _logger.LogInformation("Leave submission request received.");

        // ── 1. Parse body ────────────────────────────────────────────────────
        string body;
        try
        {
            body = await new StreamReader(req.Body).ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read request body.");
            return await BadRequest(req, "Unable to read request body.");
        }

        LeaveSubmissionRequest? request;
        try
        {
            request = JsonConvert.DeserializeObject<LeaveSubmissionRequest>(body);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON payload.");
            return await BadRequest(req, "Invalid JSON format.");
        }

        if (request?.LeaveSubmission == null)
            return await BadRequest(req, "Request body must contain a 'leaveSubmission' object.");

        // ── 2. Validate ──────────────────────────────────────────────────────
        var validationResult = await _validator.ValidateAsync(request.LeaveSubmission);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            _logger.LogError("Validation failed: {Errors}", string.Join("; ", errors));
            return await UnprocessableEntity(req, "Validation failed.", errors);
        }

        // ── 3. Process & persist ─────────────────────────────────────────────
        try
        {
            var result = await _service.ProcessAsync(request.LeaveSubmission);

            _logger.LogInformation(
                "Submission {SubmissionId} processed. {Days} day(s) persisted.",
                result.SubmissionId,
                result.WorkingDaysPersisted);

            return await OkResponse(req, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business rule violation for submission.");
            return await Conflict(req, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing leave submission.");
            return await InternalServerError(req, "An unexpected error occurred. Please try again later.");
        }
    }

    // ── Response helpers ─────────────────────────────────────────────────────

    private static async Task<HttpResponseData> OkResponse(HttpRequestData req, object body)
    {
        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonConvert.SerializeObject(body));
        return response;
    }

    private static async Task<HttpResponseData> BadRequest(HttpRequestData req, string message, List<string>? details = null)
    {
        var response = req.CreateResponse(HttpStatusCode.BadRequest);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonConvert.SerializeObject(new ErrorResponse { Error = message, Details = details }));
        return response;
    }

    private static async Task<HttpResponseData> UnprocessableEntity(HttpRequestData req, string message, List<string> details)
    {
        var response = req.CreateResponse((HttpStatusCode)422);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonConvert.SerializeObject(new ErrorResponse { Error = message, Details = details }));
        return response;
    }

    private static async Task<HttpResponseData> Conflict(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.Conflict);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonConvert.SerializeObject(new ErrorResponse { Error = message }));
        return response;
    }

    private static async Task<HttpResponseData> InternalServerError(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.InternalServerError);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonConvert.SerializeObject(new ErrorResponse { Error = message }));
        return response;
    }
}

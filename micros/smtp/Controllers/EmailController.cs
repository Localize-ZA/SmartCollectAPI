using Microsoft.AspNetCore.Mvc;
using smtp.Models;
using smtp.Services;

namespace smtp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly ITemplateService _templateService;
        private readonly ILogger<EmailController> _logger;

        public EmailController(
            IEmailService emailService,
            ITemplateService templateService,
            ILogger<EmailController> logger)
        {
            _emailService = emailService;
            _templateService = templateService;
            _logger = logger;
        }

        /// <summary>
        /// Send a single email
        /// </summary>
        [HttpPost("send")]
        public async Task<ActionResult<EmailResponse>> SendEmail([FromBody] EmailRequest request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _emailService.SendEmailAsync(request, cancellationToken);
                
                if (response.Success)
                {
                    _logger.LogInformation("Email sent successfully to {To}", request.To);
                    return Ok(response);
                }
                
                _logger.LogWarning("Failed to send email to {To}: {Error}", request.To, response.ErrorMessage);
                return BadRequest(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending email to {To}", request.To);
                return StatusCode(500, new EmailResponse
                {
                    Success = false,
                    ErrorMessage = "Internal server error occurred while sending email"
                });
            }
        }

        /// <summary>
        /// Send an email using a template
        /// </summary>
        [HttpPost("send-template")]
        public async Task<ActionResult<EmailResponse>> SendTemplateEmail([FromBody] TemplateEmailRequest request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _emailService.SendTemplateEmailAsync(request, cancellationToken);
                
                if (response.Success)
                {
                    _logger.LogInformation("Template email sent successfully to {To} using template {Template}", request.To, request.TemplateName);
                    return Ok(response);
                }
                
                _logger.LogWarning("Failed to send template email to {To}: {Error}", request.To, response.ErrorMessage);
                return BadRequest(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending template email to {To}", request.To);
                return StatusCode(500, new EmailResponse
                {
                    Success = false,
                    ErrorMessage = "Internal server error occurred while sending template email"
                });
            }
        }

        /// <summary>
        /// Send bulk emails
        /// </summary>
        [HttpPost("send-bulk")]
        public async Task<ActionResult<BulkEmailResponse>> SendBulkEmails([FromBody] BulkEmailRequest request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var response = await _emailService.SendBulkEmailsAsync(request, cancellationToken);
                _logger.LogInformation("Bulk email operation completed. Sent: {Sent}, Failed: {Failed}", 
                    response.SuccessCount, response.FailureCount);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending bulk emails");
                return StatusCode(500, new BulkEmailResponse
                {
                    SuccessCount = 0,
                    FailureCount = request.Recipients.Count,
                    Results = request.Recipients.Select(r => new EmailResult
                    {
                        Recipient = r,
                        Success = false,
                        ErrorMessage = "Internal server error"
                    }).ToList()
                });
            }
        }

        /// <summary>
        /// Get available email templates
        /// </summary>
        [HttpGet("templates")]
        public async Task<ActionResult<List<EmailTemplate>>> GetTemplates()
        {
            try
            {
                var templates = await _templateService.GetTemplatesAsync();
                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while fetching email templates");
                return StatusCode(500, "Failed to fetch email templates");
            }
        }

        /// <summary>
        /// Get email service health status
        /// </summary>
        [HttpGet("health")]
        public ActionResult<object> GetHealth()
        {
            try
            {
                var isSmtpConfigured = !string.IsNullOrEmpty(_emailService.GetSmtpHost());
                return Ok(new
                {
                    status = isSmtpConfigured ? "healthy" : "degraded",
                    smtpConfigured = isSmtpConfigured,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred during health check");
                return StatusCode(500, new
                {
                    status = "unhealthy",
                    error = ex.Message,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }
    }
}
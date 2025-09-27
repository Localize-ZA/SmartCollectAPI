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

            var response = await _emailService.SendEmailAsync(request, cancellationToken);
            
            if (response.Success)
            {
                return Ok(response);
            }
            
            return BadRequest(response);
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

            var response = await _emailService.SendTemplateEmailAsync(request, cancellationToken);
            
            if (response.Success)
            {
                return Ok(response);
            }
            
            return BadRequest(response);
        }

        /// <summary>
        /// Get available email templates
        /// </summary>
        [HttpGet("templates")]
        public async Task<ActionResult<List<string>>> GetTemplates(CancellationToken cancellationToken = default)
        {
            var templates = await _templateService.GetAvailableTemplatesAsync(cancellationToken);
            return Ok(templates);
        }

        /// <summary>
        /// Get a specific email template
        /// </summary>
        [HttpGet("templates/{templateName}")]
        public async Task<ActionResult<EmailTemplate>> GetTemplate(string templateName, CancellationToken cancellationToken = default)
        {
            var template = await _templateService.GetTemplateAsync(templateName, cancellationToken);
            
            if (template == null)
            {
                return NotFound($"Template '{templateName}' not found");
            }
            
            return Ok(template);
        }

        /// <summary>
        /// Test SMTP connection
        /// </summary>
        [HttpGet("test-connection")]
        public async Task<ActionResult<object>> TestConnection(CancellationToken cancellationToken = default)
        {
            var isConnected = await _emailService.TestConnectionAsync(cancellationToken);
            
            return Ok(new { 
                connected = isConnected, 
                message = isConnected ? "SMTP connection successful" : "SMTP connection failed",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        public async Task<ActionResult<object>> Health(CancellationToken cancellationToken = default)
        {
            var isHealthy = await _emailService.TestConnectionAsync(cancellationToken);
            
            var response = new {
                status = isHealthy ? "healthy" : "unhealthy",
                timestamp = DateTime.UtcNow,
                service = "smtp-microservice",
                version = "1.0.0"
            };

            if (isHealthy)
            {
                return Ok(response);
            }
            
            return StatusCode(503, response);
        }
    }
}

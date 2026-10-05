using System.IdentityModel.Tokens.Jwt;
using System.IO;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpCompress.Common;
using SQCScanner.Modal;
using SQCScanner.Modal.CustomTemplate;
using Version1.Data;
using static SQCScanner.Modal.CustomTemplate.TempDesignClass;

namespace SQCScanner.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CustomImg_TempController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IWebHostEnvironment env;

        public CustomImg_TempController(ILogger<CustomImg_TempController> logger, ApplicationDbContext dbContext, IWebHostEnvironment _env)
        {   
            _env = env;
            _logger = logger;
            _dbContext = dbContext;
        }

        // Static Template
        [HttpPost("CreateImgTemplate")]
        public async Task<IActionResult> CreateTemplate([FromForm] StaticImgClass model)
        {
            try
            {
                _logger.LogInformation(
                    "CreateTemplate started. TemplateId: {TemplateId}, TemplateName: {TemplateName}",
                    model.TemplateId,
                    model.TemplateName);

                var tempExist = _dbContext.ImgTemplate.FirstOrDefault(x => x.Id == int.Parse(model.TemplateId));
                if (model.TemplateImg == null || model.TemplateImg.Length == 0)
                {
                    _logger.LogWarning(
                        "CreateTemplate failed because no image was provided. TemplateId: {TemplateId}",
                        model.TemplateId);

                    return BadRequest("Template image is required.");
                }

                if(tempExist == null)
                {
                    _logger.LogWarning("CreateTemplate failed because no image was provided. TemplateId: {TemplateId}", model.TemplateId);
                    return BadRequest("Template not found.");
                }


                Console.WriteLine(tempExist.Id);
              

                using (var memoryStream = new MemoryStream())
                {
                    await model.TemplateImg.CopyToAsync(memoryStream);

                    var imageBytes = memoryStream.ToArray();
                    var base64String = Convert.ToBase64String(imageBytes);

                    var templateEntity = new StaticImgClass
                    {
                        TemplateId = $"OMR - {model.TemplateId}",
                        TemplateName = model.TemplateName,
                        TemplateImgBase64 = base64String,
                        discription = model.discription,
                    };

                    _dbContext.StaticImgClass.Add(templateEntity);
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation(
                        "Template created successfully. TemplateId: {TemplateId}, TemplateName: {TemplateName}",
                        templateEntity.TemplateId,
                        templateEntity.TemplateName);
                }

                return Ok("Template created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while creating template. TemplateId: {TemplateId}, TemplateName: {TemplateName}",
                    model?.TemplateId,
                    model?.TemplateName);

                return StatusCode(500, "An error occurred while creating the template.");
            }
        }

        [HttpPut("UpdateImgTemplate")]
        public async Task<IActionResult> UpdateImgTemplate([FromForm] StaticImgClass model)
        {
            try
            {
                _logger.LogInformation(
                    "UpdateImgTemplate started. TemplateId: {TemplateId}, TemplateName: {TemplateName}",
                    model.TemplateId,
                    model.TemplateName);

                // Validate TemplateId
               

                // Find existing template
                var templateEntity = await _dbContext.StaticImgClass.FirstOrDefaultAsync(t => t.Id == model.Id);
                if (templateEntity == null)
                {
                    _logger.LogWarning(
                        "UpdateImgTemplate failed. TemplateId: {TemplateId} not found.",
                        model.TemplateId);

                    return NotFound("Record not found.");
                }
                // 1. Update Template Name
                if (!string.IsNullOrWhiteSpace(model.TemplateName))
                {
                    templateEntity.TemplateName = model.TemplateName;
                }
                // 2. TemplateId change
                if (!string.IsNullOrWhiteSpace(model.TemplateId))
                {
                    templateEntity.TemplateId = $"OMR - {model.TemplateId}";
                }
                // 3. Discription chamge
                if (!string.IsNullOrWhiteSpace(model.discription))
                {
                    templateEntity.discription = model.discription;
                }

                // 4. Update Image if new image is provided
                if (model.TemplateImg != null && model.TemplateImg.Length > 0)
                {
                    using var memoryStream = new MemoryStream();
                    await model.TemplateImg.CopyToAsync(memoryStream);
                    var imageBytes = memoryStream.ToArray();
                    templateEntity.TemplateImgBase64 = Convert.ToBase64String(imageBytes);
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Template updated successfully. TemplateId: {TemplateId}",
                    model.TemplateId);

                return Ok(new
                {
                    success = true,
                    message = "Template updated successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while updating template. TemplateId: {TemplateId}, TemplateName: {TemplateName}",
                    model?.TemplateId,
                    model?.TemplateName);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while updating the template.");
            }
        }

        [HttpGet("ViewImgTempale")]
        public IActionResult GetAllTemplates(int? tempId)
        {
            try
            {
                object templates;
                if (tempId == null)
                {
                    templates = _dbContext.StaticImgClass.ToList();
                }
                else
                {
                    templates = _dbContext.StaticImgClass.FirstOrDefault(a=>a.Id==tempId);
                }

                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving templates.");
                return StatusCode(500, "An error occurred while retrieving templates.");
            }
        }

        [HttpDelete("DeleteImgTempale")]
        public async Task<IActionResult> DeleteImageTemplate([FromQuery] string templateIdVal)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(templateIdVal))
                {
                    return BadRequest("Template ID is required.");
                }

                var template = await _dbContext.StaticImgClass.FirstOrDefaultAsync(t => t.Id == int.Parse(templateIdVal));

                if (template == null)
                {
                    _logger.LogWarning(
                        "DeleteImageTemplate failed. TemplateId: {TemplateId} not found.",
                        templateIdVal);

                    return NotFound("Template not found.");
                }

                _dbContext.StaticImgClass.Remove(template);

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Template deleted successfully. TemplateId: {TemplateId}",
                    templateIdVal);

                return Ok(new
                {
                    success = true,
                    message = "Template deleted successfully."
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "DeleteImageTemplate request was cancelled. TemplateId: {TemplateId}",
                    templateIdVal);

                return StatusCode(499, "Request cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while deleting template. TemplateId: {TemplateId}",
                    templateIdVal);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while deleting the template.");
            }
        }


        // Dynamic Template Design
        [HttpPost("CreateImgTempDesing")]
        public async Task<IActionResult> CreateImgTempDesing([FromForm] StaticImgDesign model)
        {
            try
            {
                _logger.LogInformation(
                   "CreateTemplate started. TemplateId: {TemplateId}, TemplateName: {TemplateName}",
                   model.TemplateId,
                   model.TemplateName);
                  var getToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
                  if (string.IsNullOrWhiteSpace(getToken))
                  {
                      return Unauthorized(new { message = "No token provided" });
                  }
                  var handler = new JwtSecurityTokenHandler();
                  var TokenDecription = handler.ReadJwtToken(getToken);
                  var Empid = TokenDecription.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;

                // Save Record Images
                string uploadsFolder = Path.Combine(env.WebRootPath, "ImageManager");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                    _logger.LogInformation("ImageManager folder created.");
                }

                // Save JSON
                if (!string.IsNullOrEmpty(model.TemplateJsonFile.FileName))
                {
                    string tempFolder = Path.Combine(env.WebRootPath, "TempManager");
                    string jsonFileName = Path.GetFileName(model.TemplateJsonFile.FileName);
                    string jsonFilePath = Path.Combine(tempFolder, jsonFileName);
                    using (var tempSet = new FileStream(jsonFilePath, FileMode.Create))
                    {
                        await model.TemplateJsonFile.CopyToAsync(tempSet);
                    }
                }


                using (var memoryStream = new MemoryStream())
                {
                    await model.TemplateLogo.CopyToAsync(memoryStream);
                    var imageBytes = memoryStream.ToArray();
                    var base64String = Convert.ToBase64String(imageBytes);

                    var templateEnA = new StaticImgDesign
                    {
                        TemplateImage = base64String,
                        TemplateJSON = base64String,
                        Discription = model.Discription,
                        TemplateId = model.TemplateId,
                        TemplateName = model.TemplateName
                    };
                    _dbContext.staticImgDesigns.Add(templateEnA);
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation(
                       "Template created successfully. TemplateId: {TemplateId}, TemplateName: {TemplateName}",
                       templateEnA.TemplateId,
                       templateEnA.TemplateName);

                }
            }
            catch (Exception ex) { 
            
            }







            return Ok();
        }



    }
}

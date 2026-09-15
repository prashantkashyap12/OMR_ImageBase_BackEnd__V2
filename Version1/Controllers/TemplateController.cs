using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using Serilog.Parsing;
using SharpCompress.Archives;
using SQCScanner.Modal;
using SQCScanner.Services;
using SQCScanner.websoketManager;
using Syncfusion.Data;
using Syncfusion.EJ2.Notifications;
using Version1.Data;
using Version1.Modal;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static OpenCvSharp.XImgProc.CvXImgProc;
using SharpCompress.Archives;
using Microsoft.Extensions.Logging;

namespace SQCScanner.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [EnableCors("AllowAnyOrigin")]
    [ApiController]
    public class TemplateController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly string _root = Environment.CurrentDirectory + "/wFileManager";
        private readonly WebSoketHandler _webSocketHandler;

        public TemplateController(IWebHostEnvironment env, ApplicationDbContext dbContext, ILogger<TemplateController> logger, IConfiguration configuration, WebSoketHandler webSocketHandler)
        {
            _env = env;
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("dbc");
            _webSocketHandler = webSocketHandler;
        }


        [HttpPost]
        [Route("Create_ImeTemp")]
        public async Task<IActionResult> imgRec(IFormFile ImgTemp, string TempName, string empId, string? description)
        {
            dynamic res;
            bool exist = true;

            _logger.LogInformation("Create_ImeTemp API called. TempName: {TempName}, EmpId: {EmpId}", TempName, empId);
            if (ImgTemp == null || ImgTemp.Length == 0)
            {
                _logger.LogWarning("No file uploaded. TempName: {TempName}", TempName);
                res = new { state = false, message = "No file uploaded." };
            }
            else
            {
                string extension = Path.GetExtension(ImgTemp.FileName).ToLower();

                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                {
                    _logger.LogWarning("Invalid file type uploaded: {FileName}", ImgTemp.FileName);

                    res = new
                    {
                        state = false,
                        message = "Invalid file type. Only .jpg, .jpeg, or .png are allowed."
                    };
                }
                else
                {
                    try
                    {
                        description ??= "";

                        _logger.LogInformation("Image validation completed for {FileName}", ImgTemp.FileName);

                        string fileName = Path.GetFileName(ImgTemp.FileName);

                        var TempNameUnq = _dbContext.ImgTemplate
                                                    .Select(x => x.FileName)
                                                    .ToList();

                        foreach (var tempUnq in TempNameUnq)
                        {
                            if (tempUnq == TempName)
                            {
                                exist = false;
                                break;
                            }
                        }

                        if (exist)
                        {
                            string uploadsFolder = Path.Combine(_env.WebRootPath, "ImageManager");

                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);

                                _logger.LogInformation("ImageManager folder created.");
                            }

                            string ImgfileName = Path.GetFileName(ImgTemp.FileName);

                            string filePath = Path.Combine(uploadsFolder, ImgfileName);

                            string relativeImgPath = Path.Combine("ImageManager", ImgfileName)
                                .Replace("\\", "/");

                            var fileExists = await _dbContext.ImgTemplate
                                .AnyAsync(t => t.FileName == fileName);

                            var results = new List<ImgTemp>();

                            if (!fileExists)
                            {
                                using (var fs = new FileStream(filePath, FileMode.Create))
                                {
                                    await ImgTemp.CopyToAsync(fs);
                                }

                                _logger.LogInformation("Image saved successfully at {Path}", filePath);

                                DateTime date = DateTime.Now;
                                string fileNameMix = $"{TempName}##{empId}";

                                var resp = _dbContext.Add(new ImgTemp
                                {
                                    FileName = fileNameMix,
                                    imgPath = relativeImgPath,
                                    JsonPath = "",
                                    CreateAt = date.ToString(),
                                    discription = description
                                });

                                await _dbContext.SaveChangesAsync();

                                _logger.LogInformation(
                                    "Database record created successfully. TempName: {TempName}, Employee: {EmpId}",
                                    TempName,
                                    empId);

                                results.Add(resp.Entity);

                                res = new
                                {
                                    message = "File Save into Table and Folder",
                                    status = true,
                                    data = results
                                };
                            }
                            else
                            {
                                _logger.LogWarning("File already exists in database. FileName: {FileName}", fileName);

                                res = new
                                {
                                    message = "File Not Exist",
                                    status = false,
                                };
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Template name already exists. TempName: {TempName}", TempName);

                            res = new
                            {
                                state = false,
                                message = "FileName is Already Exist"
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Exception occurred while uploading image. TempName: {TempName}, EmpId: {EmpId}",
                            TempName,
                            empId);

                        res = new
                        {
                            message = ex.Message,
                            status = false,
                        };
                    }
                }
            }

            bool currentState = res.status;

            if (currentState)
            {
                _logger.LogInformation("Create_ImeTemp API completed successfully.");
                return Ok(res);
            }
            else
            {
                _logger.LogWarning("Create_ImeTemp API failed.");
                return NotFound(res);
            }
        }

        // DONE - -
        [HttpGet]
        [Route("Single_ImeTem")]
        public async Task<IActionResult> getImgTemp(int id)
        {
            dynamic res;
            try
            {
                var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.Id == id);
                if (ReturnDetails != null)
                {
                    if (!string.IsNullOrEmpty(ReturnDetails.imgPath))
                    {
                        ReturnDetails.imgPath = ReturnDetails.imgPath.Replace("\\", "/");
                    }
                    if (!string.IsNullOrEmpty(ReturnDetails.JsonPath))
                    {
                        ReturnDetails.JsonPath = ReturnDetails.JsonPath.Replace("\\", "/");
                    }
                    res = new
                    {
                        data = ReturnDetails,
                        state = true,
                        Message = "Record Found"
                    };
                }
                else
                {
                    res = new
                    {
                        state = false,
                        Message = "Record Not Found",
                    };
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message,
                };
            }
            bool currentStatex = res.state;
            if (currentStatex)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        // DONE - -
        [HttpGet]
        [Route("List_ImeTemp")]
        public async Task<IActionResult> getImgTemp()
        {
            dynamic res;
            try
            {
                var results = await _dbContext.ImgTemplate.ToListAsync();
                if (results.Count == null || results.Count == 0)
                {
                    res = new
                    {
                        state = false,
                        message = "Record Not Found"
                    };
                }
                else
                {
                    foreach (var item in results)
                    {
                        if (!string.IsNullOrEmpty(item.imgPath))
                        {
                            item.imgPath = item.imgPath.Replace("\\", "/");
                        }
                        if (!string.IsNullOrEmpty(item.JsonPath))
                        {
                            item.JsonPath = item.JsonPath.Replace("\\", "/");
                        }
                    }
                    res = new
                    {
                        state = true,
                        message = "Record Found",
                        body = results
                    };
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    message = ex.Message,
                };
            }
            bool currentState = res.state;
            if (currentState)
            {
                return Ok(res);
            }
            else
            {
                return NotFound(res);
            }
        }

        [HttpDelete]
        [Route("Del_ImeTemp")]
        public async Task<IActionResult> deleteTemp(int id)
        {
            dynamic res;

            _logger.LogInformation("Delete template request received. TemplateId: {TemplateId}", id);

            try
            {
                // Find record
                var tempRecord = await _dbContext.ImgTemplate.FindAsync(id);

                if (tempRecord == null)
                {
                    _logger.LogWarning("Template not found. TemplateId: {TemplateId}", id);

                    res = new
                    {
                        state = false,
                        message = "Template not found."
                    };
                }
                else
                {
                    _logger.LogInformation(
                        "Deleting template. TemplateId: {TemplateId}, FileName: {FileName}",
                        id,
                        tempRecord.FileName);

                    // Delete DB Record
                    _dbContext.ImgTemplate.Remove(tempRecord);
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation("Database record deleted successfully. TemplateId: {TemplateId}", id);

                    // Delete Image
                    string imgFolder = Path.Combine(_env.WebRootPath, "ImageManager");
                    string imgFileName = Path.GetFileName(tempRecord.imgPath);
                    string imgFilePath = Path.Combine(imgFolder, imgFileName);

                    if (System.IO.File.Exists(imgFilePath))
                    {
                        await Task.Run(() => System.IO.File.Delete(imgFilePath));

                        _logger.LogInformation("Image deleted successfully. Path: {ImagePath}", imgFilePath);
                    }
                    else
                    {
                        _logger.LogWarning("Image file not found. Path: {ImagePath}", imgFilePath);
                    }

                    // Delete JSON
                    if (!string.IsNullOrEmpty(tempRecord.JsonPath))
                    {
                        string tempFolder = Path.Combine(_env.WebRootPath, "TempManager");
                        string jsonFileName = Path.GetFileName(tempRecord.JsonPath);
                        string jsonFilePath = Path.Combine(tempFolder, jsonFileName);

                        if (System.IO.File.Exists(jsonFilePath))
                        {
                            await Task.Run(() => System.IO.File.Delete(jsonFilePath));

                            _logger.LogInformation("JSON file deleted successfully. Path: {JsonPath}", jsonFilePath);
                        }
                        else
                        {
                            _logger.LogWarning("JSON file not found. Path: {JsonPath}", jsonFilePath);
                        }
                    }

                    res = new
                    {
                        message = "Template delete from database and fileManager.",
                        state = true
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception occurred while deleting template. TemplateId: {TemplateId}",
                    id);

                res = new
                {
                    message = ex.Message,
                    state = false
                };
            }

            if (res.state)
            {
                _logger.LogInformation("Delete template completed successfully. TemplateId: {TemplateId}", id);
                return Ok(res);
            }
            else
            {
                _logger.LogWarning("Delete template failed. TemplateId: {TemplateId}", id);
                return NotFound(res);
            }
        }

        [HttpPut]
        [Route("Update_ImeTemp")]
        public async Task<IActionResult> Update(string FileName, IFormFile tempName)
        {
            dynamic res;

            _logger.LogInformation("Update_ImeTemp API called. Template: {FileName}", FileName);

            try
            {
                if (tempName != null && Path.GetExtension(tempName.FileName).ToLower() == ".json")
                {
                    _logger.LogInformation("JSON file validation passed. Uploaded File: {UploadedFile}", tempName.FileName);

                    // Save new JSON file into Folder
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "TempManager");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                        _logger.LogInformation("TempManager folder created.");
                    }

                    var fileName = Path.GetFileName(tempName.FileName);
                    string filePath = Path.Combine(uploadsFolder, fileName);
                    string relativePath = Path.Combine("TempManager", fileName).Replace("\\", "/");

                    var exists = await _dbContext.ImgTemplate.AnyAsync(t => t.FileName == FileName);

                    if (exists)
                    {
                        var ReturnDetails = _dbContext.ImgTemplate.FirstOrDefault(x => x.FileName == FileName);

                        var oldJsonPath = ReturnDetails.JsonPath;

                        ReturnDetails.JsonPath = relativePath;
                        ReturnDetails.CreateAt = DateTime.Now.ToString();

                        await _dbContext.SaveChangesAsync();

                        _logger.LogInformation("Database updated successfully for Template: {FileName}", FileName);

                        // Delete old JSON
                        var oldFullPath = Path.Combine(
                            _env.WebRootPath,
                            oldJsonPath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));

                        if (System.IO.File.Exists(oldFullPath))
                        {
                            System.IO.File.Delete(oldFullPath);

                            _logger.LogInformation("Old JSON deleted: {OldFile}", oldJsonPath);
                        }
                        else
                        {
                            _logger.LogWarning("Old JSON file not found: {OldFile}", oldJsonPath);
                        }

                        // Save New JSON
                        using (var TempSet = new FileStream(filePath, FileMode.Create))
                        {
                            await tempName.CopyToAsync(TempSet);
                        }

                        _logger.LogInformation("New JSON saved successfully at {Path}", filePath);

                        res = new
                        {
                            message = "JSON Template SAVE",
                            state = true
                        };
                    }
                    else
                    {
                        _logger.LogWarning("Template not found in database. FileName: {FileName}", FileName);

                        res = new
                        {
                            message = "Template not found in database.",
                            state = false
                        };
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid file uploaded. Only JSON files are allowed.");

                    res = new
                    {
                        message = "Template not JSON.",
                        state = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error while updating template. FileName: {FileName}",
                    FileName);

                res = new
                {
                    message = ex.Message,
                    state = false,
                };
            }

            if (res.state)
            {
                _logger.LogInformation("Update_ImeTemp completed successfully. Template: {FileName}", FileName);
                return Ok(res);
            }
            else
            {
                _logger.LogWarning("Update_ImeTemp failed. Template: {FileName}", FileName);
                return NotFound(res);
            }
        }

        // Test Management -- Store procedure //
        [HttpPost]
        [Route("CreateTest")]
        public async Task<IActionResult> CreateTest([FromBody] TestCreate model)
        {
            dynamic res;
            try
            {
                var getToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
                if (string.IsNullOrWhiteSpace(getToken))
                {
                    return Unauthorized(new { message = "No token provided" });
                }
                var handler = new JwtSecurityTokenHandler();
                var TokenDecription = handler.ReadJwtToken(getToken);
                var Empid = TokenDecription.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                var directoryPath = Path.Combine(_root, Empid, model.testName);
                if (!string.IsNullOrWhiteSpace(model.TemplateId))
                {
                    int tempId = Convert.ToInt32(model.TemplateId);
                    var data = _dbContext.ImgTemplate.FirstOrDefault(x => x.Id == tempId);
                    Console.WriteLine();
                    if (!string.IsNullOrEmpty(data.JsonPath) == null && data.JsonPath == "")
                    {
                        BadRequest("Template not Found");
                    }
                }
                using (var _conn = new SqlConnection(_connectionString))
                {
                    await _conn.OpenAsync();
                     var querryA = $"SELECT * FROM TestCases WHERE TestName = '{model.testName}' AND EmpId = '{Empid}'";
                    var isExist = _conn.QueryFirstOrDefault<TestCreate>(querryA);
                        var firstValue = isExist?.TestId.Split('/')[0];

                    if(isExist == null)
                    {
                        var querry = $"insert into TestCases (TemplateId, TestName, TestId, Notes, status, EmpId) values ('{model.TemplateId}', '{model.testName}', '{model.TestId}', '{model.notes}', '{model.status}', '{Empid}')";
                        _conn.ExecuteAsync(querry);
                        if (!Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }
                        await _conn.CloseAsync();
                        res = new { state = true, message = "Test created successfully" };
                    }
                    else{
                        if (firstValue == Empid)
                        {
                            return Conflict(new
                            {
                                state = false,
                                message = "Test name already exist, please change Test Name"
                            });
                        }
                        else
                        {
                            var querry = $"insert into TestCases (TemplateId, TestName, TestId, Notes, status) values ('{model.TemplateId}', '{model.testName}', '{model.TestId}', '{model.notes}', '{model.status}')";
                            _conn.ExecuteAsync(querry);
                            if (!Directory.Exists(directoryPath))
                            {
                                Directory.CreateDirectory(directoryPath);
                            }
                            await _conn.CloseAsync();
                            res = new { state = true, message = "Test created successfully" };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                res = new { state = false, message = ex.Message };
            }

            return Ok(res);
        }

        //[HttpPost]
        //[Route("GetTest")]
        //public async Task<IActionResult> GetTest([FromBody] ListCount model)
        //{
        //    dynamic res;
        //    try
        //    {
        //        var tests = new List<TestCreate>();

        //        using (var _conn = new SqlConnection(_connectionString))
        //        {
        //            _conn.Open();
        //            string query = $"SELECT * FROM TestCases ";
        //            var data = await _conn.QueryAsync<TestCreate>(query);
        //            var totalcout = data.Count();
        //            if (!string.IsNullOrEmpty(model.search))
        //            {
        //                data = data.Where(x => x.TestId.Contains(model.search) || x.testName.Contains(model.search));
        //                Console.WriteLine(data);
        //            }
        //            else
        //            {
        //                data = data.OrderBy(x => x.Sr).Skip((model.page - 1) * model.range).Take(int.Parse(model.range.ToString()));
        //            }
        //            res = new { state = true, message = "Data List", Record = data, count = totalcout };
        //            _conn.Close();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        res = new { state = false, Message = ex.Message };
        //    }
        //    return Ok(res);
        //}
        //public class ListCount
        //{
        //    public int page { get; set; }
        //    public int range { get; set; }
        //    public string search { get; set; }
        //}


        [HttpPost]
        [Route("GetTest")]
        public async Task<IActionResult> GetTest([FromBody] ListCount model)
        {
            dynamic res;

            try
            {
                using (var _conn = new SqlConnection(_connectionString))
                {
                    _conn.Open();

                    string query = "SELECT * FROM TestCases";

                    var data = await _conn.QueryAsync<TestCreate>(query);

                    var totalcount = data.Count();

                    if (!string.IsNullOrEmpty(model.search))
                    {
                        string search = model.search.ToLower();

                        data = data.Where(x =>
                            (!string.IsNullOrEmpty(x.TestId) && x.TestId.ToLower().Contains(search)) ||
                            (!string.IsNullOrEmpty(x.testName) && x.testName.ToLower().Contains(search))
                        );
                    }
                    else
                    {
                        data = data
                            .OrderBy(x => x.Sr)
                            .Skip((model.page - 1) * model.range)
                            .Take(model.range);
                    }

                    res = new
                    {
                        state = true,
                        message = "Data List",
                        Record = data,
                        count = totalcount
                    };

                    _conn.Close();
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    state = false,
                    Message = ex.Message
                };
            }

            return Ok(res);
        }

        public class ListCount
        {
            public int page { get; set; }
            public int range { get; set; }
            public string search { get; set; }
        }


        [HttpDelete]
        [Route("DeleteTest")]
        public async Task<IActionResult> DeleteTest(string testId)
        {
            dynamic res;
            try
            {
                //using (SqlConnection connection = getConnection())
                //{
                //    await connection.OpenAsync();

                //    using (SqlCommand command = new SqlCommand("TestCases_Proc", connection))
                //    {
                //        command.CommandType = System.Data.CommandType.StoredProcedure;

                //        command.Parameters.AddWithValue("@Action", "Delete");
                //        command.Parameters.AddWithValue("@TestId", testId);
                //        await command.ExecuteNonQueryAsync();
                //    }
                //}
                var getToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
                var handler = new JwtSecurityTokenHandler();
                var TokenDecription = handler.ReadJwtToken(getToken);
                var Empid = TokenDecription.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                Console.WriteLine(Empid);
                using (var _conn = new SqlConnection(_connectionString))
                {
                    _conn.Open();
                    var dataTest = _conn.QueryFirstOrDefault<TestCreate>($"select * from TestCases where TestId = '{testId}'");
                    var directoryPath = "";
                    if (dataTest != null)
                    {
                        var data = await _conn.ExecuteAsync($"delete TestCases where TestId = '{testId}'");
                        directoryPath = Path.Combine(_root, Empid, dataTest.testName);
                        if (Directory.Exists(directoryPath))
                        {
                            Directory.Delete(directoryPath);
                            res = new { state = true, message = $"'{dataTest.testName}' Test and his Images deleted successfully" };
                        }
                        else
                        {
                            res = new { state = true, message = $"{dataTest.testName} Test deleted successfully" };
                        }

                    }
                    else
                    {
                        Console.WriteLine(directoryPath);
                        res = new { state = true, message = $"Test ({testId}) related Data not found in our records" };
                    }
                    _conn.Close();
                }
            }
            catch (Exception ex)
            {
                res = new { state = true, message = ex.Message };
            }
            return Ok(res);
        }

        [HttpPut]
        [Route("UpdateTest")]
        public async Task<IActionResult> UpdateTest([FromBody] TestCreate model)
        {
            dynamic res;
            try
            {
                using (var _conn = new SqlConnection(_connectionString))
                {
                    _conn.Open();
                    var isExist = _conn.ExecuteScalar<int>($"select COUNT(1) from TestCases where TestId = '{model.TestId}'");
                    if (isExist > 0)
                    {
                        var qurry = $"update TestCases set TemplateId = '{model.TemplateId}', TestName = '{model.testName}', Notes = '{model.notes}', [status] = '{model.status}' where TestId = '{model.TestId}'";
                        _conn.Execute(qurry);
                        res = new { state = true, message = "Test updated successfully" };
                        _conn.Close();
                    }
                    else
                    {
                        res = new { state = false, message = "Test not found" };
                    }
                }
            }
            catch (Exception ex)
            {
                res = new { state = false, message = ex.Message };
            }
            return Ok(res);
        }
        public class TestCreate
        {
            public int? Sr {get; set;}
            public string? TemplateId { get; set; }
            public string? testName { get; set; }
            public string? TestId { get; set; }
            public string? notes { get; set; }
            public string? status { get; set; }
        }

        // Image upload Realtime api.
        //[HttpPost("upload")]
        //public async Task<IActionResult> UploadImage(List<IFormFile> files, string TestName)
        //{
        //    if (files == null || files.Count == 0)
        //        return BadRequest("Image is required.");

        //    string directoryPath = "";
        //    var getToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
        //    var handler = new JwtSecurityTokenHandler();
        //    var TokenDecription = handler.ReadJwtToken(getToken);
        //    var Empid = TokenDecription.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
        //    Console.WriteLine(Empid);
        //    using(var _conn = new SqlConnection(_connectionString))
        //    {
        //        var dataTest = _conn.QueryFirstOrDefault<TestCreate>($"select * from TestCases where TestName = '{TestName}'");
        //        directoryPath = Path.Combine(_root, Empid, dataTest.testName);
        //    }

        //    if (!Directory.Exists(directoryPath))
        //    {
        //        Directory.CreateDirectory(directoryPath);
        //    }

        //    //var uploadedFiles = new List<object>();
        //    int uploadedCount = 0;
        //    foreach (var file in files)
        //    {
        //        if (file == null || file.Length == 0)
        //            continue;

        //        string fileName = $"OMRIOS_{file.FileName}";

        //        string filePath = Path.Combine(directoryPath, fileName);
        //        using (var stream = new FileStream(filePath, FileMode.Create))
        //        {
        //            await file.CopyToAsync(stream);
        //            uploadedCount++;
        //        }
        //        var uploadedFiles = new
        //        {
        //            type = $"{fileName} Image file uploaded successfully",
        //            path = filePath,
        //            uploadedCount = uploadedCount,
        //            totalFiles = files.Count
        //        };
        //        string json = JsonSerializer.Serialize(uploadedFiles);
        //        await _webSocketHandler.UserMessageAsync(Empid, json);
        //    }
        //    await _webSocketHandler.UserMessageAsync(Empid, "");
        //    return Ok(new
        //    {
        //        success = true,
        //        message = "Image uploaded successfully",
        //    });
        //}

        //[RequestSizeLimit(2_000_000_000)]
        //[RequestFormLimits(MultipartBodyLengthLimit = 2_000_000_000)]
        //[HttpPost("upload")]
        //public async Task<IActionResult> UploadImage(List<IFormFile> files, string TestName)
        //{  
        //    try
        //    {
        //        // ---------------------------------------------
        //        // 1. Validate files
        //        // ---------------------------------------------
        //        if (files == null || files.Count == 0)
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                message = "Image is required."
        //            });
        //        }

        //        if (string.IsNullOrWhiteSpace(TestName))
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                message = "TestName is required."
        //            });
        //        }

        //        // ---------------------------------------------
        //        // 2. Get Employee ID from JWT
        //        // ---------------------------------------------
        //        var token = Request.Headers["Authorization"]
        //            .FirstOrDefault()?
        //            .Replace("Bearer ", "")
        //            .Trim();

        //        if (string.IsNullOrWhiteSpace(token))
        //        {
        //            return Unauthorized(new
        //            {
        //                success = false,
        //                message = "Authorization token is missing."
        //            });
        //        }

        //        var handler = new JwtSecurityTokenHandler();
        //        var tokenDescription = handler.ReadJwtToken(token);

        //        var empId = tokenDescription.Claims
        //            .FirstOrDefault(c => c.Type == "nameid")
        //            ?.Value;

        //        if (string.IsNullOrWhiteSpace(empId))
        //        {
        //            return Unauthorized(new
        //            {
        //                success = false,
        //                message = "Employee ID not found in token."
        //            });
        //        }

        //        // ---------------------------------------------
        //        // 3. Get Test from DB
        //        // ---------------------------------------------
        //        TestCreate? dataTest;

        //        using (var conn = new SqlConnection(_connectionString))
        //        {
        //            conn.Open();
        //            dataTest = conn.QueryFirstOrDefault<TestCreate>(@"SELECT * FROM TestCases WHERE TestName = @TestName", new { TestName });
        //            conn.Close();
        //        }

        //        if (dataTest == null){
        //            return NotFound(new
        //            {
        //                success = false,
        //                message = $"Test '{TestName}' not found."
        //            });
        //        }

        //        // ---------------------------------------------
        //        // 4. Build directory
        //        // ---------------------------------------------
        //        string directoryPath = Path.Combine(_root, empId, dataTest.testName);

        //        // IMPORTANT:
        //        Directory.CreateDirectory(directoryPath);

        //        // ---------------------------------------------
        //        // 5. Upload files
        //        // ---------------------------------------------
        //        int uploadedCount = 0;

        //        const long maxImageSize = 2L * 1024 * 1024 * 1024; // 20 GB

        //        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };
        //        var run = 0;

        //        foreach (var file in files)
        //        {
        //            if (file == null || file.Length == 0)
        //                continue;

        //            // 20 MB per image
        //            if (file.Length > maxImageSize)
        //            {
        //                return BadRequest(new
        //                {
        //                    success = false,
        //                    message = $"File '{file.FileName}' exceeds the maximum allowed size of 20 MB."
        //                });
        //            }


        //            // NEVER directly trust file.FileName
        //            string originalFileName = Path.GetFileName(file.FileName);

        //            // Get extension
        //            string extension = Path.GetExtension(originalFileName);

        //            // Extenstion not matched
        //            if (!allowedExtensions.Contains(extension))
        //            {
        //                return BadRequest(new
        //                {
        //                    success = false,
        //                    message = $"File '{originalFileName}' is not a supported image format."
        //                });
        //            }


        //            // Generate a SHORT safe filename
        //            string fileName =
        //                $"OMRIOS_{Guid.NewGuid():N}{extension}";

        //            string filePath = Path.Combine(
        //                directoryPath,
        //                fileName
        //            );

        //            Console.WriteLine($"Original File : {originalFileName}");
        //            Console.WriteLine($"Saving File   : {fileName}");
        //            Console.WriteLine($"Full Path     : {filePath}");

        //            // -----------------------------------------
        //            // 6. Save file
        //            // -----------------------------------------
        //            await using (var stream = new FileStream(
        //                filePath,
        //                FileMode.CreateNew,
        //                FileAccess.Write,
        //                FileShare.None,
        //                81920,
        //                useAsync: true))
        //            {
        //                await file.CopyToAsync(stream);
        //            }

        //            uploadedCount++;

        //            // -----------------------------------------
        //            // 7. WebSocket notification
        //            // -----------------------------------------
        //            var uploadedFile = new
        //            {
        //                type = $"{fileName} Image file uploaded successfully",
        //                path = filePath,
        //                uploadedCount = uploadedCount,
        //                totalFiles = files.Count
        //            };

        //            string json = JsonSerializer.Serialize(uploadedFile);

        //             _webSocketHandler.UserMessageAsync(
        //                empId,
        //                json
        //            );
        //            run++;
        //            if (run == 1)
        //            {
        //                using (var conn = new SqlConnection(_connectionString))
        //                {
        //                    conn.Open();
        //                    var qry = "UPDATE TestCases SET status = @Status WHERE TestName = @TestName";
        //                    conn.Execute(qry, new
        //                    {
        //                        Status = "Y",
        //                        TestName = TestName
        //                    });
        //                    conn.Close();
        //                }
        //            }
        //        }

        //        // Finish WebSocket progress
        //         _webSocketHandler.UserMessageAsync(
        //            empId,
        //            ""
        //        );



        //        return Ok(new
        //        {
        //            success = true,
        //            message = "Image uploaded successfully.",
        //            uploadedCount = uploadedCount,
        //            totalFiles = files.Count
        //        });
        //    }
        //    catch (Exception ex){
        //        Console.WriteLine(ex);
        //        return StatusCode(500, new
        //        {
        //            success = false,
        //            message = "Image upload failed.",
        //            error = ex.Message
        //        });
        //    }
        //}

        [RequestSizeLimit(3_221_225_472)]
        [RequestFormLimits(MultipartBodyLengthLimit = 3_221_225_472, ValueLengthLimit = int.MaxValue)]
        [HttpPost("upload")]
        public async Task<IActionResult> UploadArchiveFiles(List<IFormFile> files, string TestName)
        {
            _logger.LogInformation("UploadArchiveFiles execution started for TestName: {TestName}", TestName);

            try
            {
                // ---------------------------------------------
                // 1. Validate input files presence & TestName
                // ---------------------------------------------
                if (files == null || files.Count == 0)
                {
                    _logger.LogWarning("Upload failed: No files were provided.");
                    return BadRequest(new { success = false, message = "Please attach at least one .zip or .rar file." });
                }

                if (string.IsNullOrWhiteSpace(TestName))
                {
                    _logger.LogWarning("Upload failed: TestName is null or empty.");
                    return BadRequest(new { success = false, message = "TestName is required." });
                }

                // ---------------------------------------------
                // 2. Validate strict archive format (.zip or .rar ONLY)
                // ---------------------------------------------
                var allowedArchiveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar"
        };

                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file.FileName);
                    if (string.IsNullOrEmpty(ext) || !allowedArchiveExtensions.Contains(ext))
                    {
                        _logger.LogWarning("Invalid file upload attempt: {FileName}. Extension '{Extension}' is not allowed.", file.FileName, ext);
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Invalid file format '{file.FileName}'. Only .zip and .rar compressed archive files are allowed."
                        });
                    }
                }

                // ---------------------------------------------
                // 3. Get Employee ID from JWT
                // ---------------------------------------------
                var token = Request.Headers["Authorization"]
                    .FirstOrDefault()?
                    .Replace("Bearer ", "")
                    .Trim();

                if (string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogWarning("Upload failed: Authorization token missing.");
                    return Unauthorized(new { success = false, message = "Authorization token is missing." });
                }

                var handler = new JwtSecurityTokenHandler();
                var tokenDescription = handler.ReadJwtToken(token);

                var empId = tokenDescription.Claims
                    .FirstOrDefault(c => c.Type == "nameid")
                    ?.Value;

                if (string.IsNullOrWhiteSpace(empId))
                {
                    _logger.LogWarning("Upload failed: Employee ID (nameid claim) not found in JWT token.");
                    return Unauthorized(new { success = false, message = "Employee ID not found in token." });
                }

                // ---------------------------------------------
                // 4. Get Test from DB
                // ---------------------------------------------
                TestCreate? dataTest;
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    dataTest = await conn.QueryFirstOrDefaultAsync<TestCreate>(
                        @"SELECT * FROM TestCases WHERE TestName = @TestName", new { TestName });
                    conn.Close();
                }

                if (dataTest == null)
                {
                    _logger.LogWarning("TestName: {TestName} not found in TestCases table.", TestName);
                    return NotFound(new { success = false, message = $"Test '{TestName}' not found." });
                }

                // ---------------------------------------------
                // 5. Directory Path setup
                // ---------------------------------------------
                string directoryPath = Path.Combine(_root, empId, dataTest.testName);
                Directory.CreateDirectory(directoryPath);
                _logger.LogInformation("Target directory path created/verified: {DirectoryPath}", directoryPath);

                // ---------------------------------------------
                // 6. Extraction Logic
                // ---------------------------------------------
                int extractedImagesCount = 0;
                var allowedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"
        };

                bool isFirstArchiveProcess = true;

                foreach (var archiveFile in files)
                {
                    if (archiveFile == null || archiveFile.Length == 0)
                        continue;

                    _logger.LogInformation("Processing archive file: {FileName}, Size: {Length} bytes", archiveFile.FileName, archiveFile.Length);

                    try
                    {
                        using (var archiveStream = archiveFile.OpenReadStream())
                        //using (var archive = ArchiveFactory.Open(archiveStream))
                        using (var archive = ArchiveFactory.OpenArchive(archiveStream))
                        {
                            foreach (var entry in archive.Entries)
                            {
                                if (entry.IsDirectory)
                                    continue;

                                string originalFileName = Path.GetFileName(entry.Key);
                                string extension = Path.GetExtension(originalFileName);
                                var dataTo = archive.Entries.Count();
                                Console.WriteLine(dataTo);

                                if (allowedImageExtensions.Contains(extension))
                                {
                                    string newFileName = $"OMRIOS_{Guid.NewGuid():N}{extension}";
                                    string filePath = Path.Combine(directoryPath, newFileName);

                                    await using (var entryStream = entry.OpenEntryStream())
                                    await using (var targetStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                                    {
                                        await entryStream.CopyToAsync(targetStream);
                                    }

                                    extractedImagesCount++;
                                    var progressNotification = new
                                    {
                                        type = $"{newFileName} Image extracted successfully",
                                        path = filePath,
                                        uploadedCount = extractedImagesCount,
                                        Total = extractedImagesCount == 1 ? dataTo.ToString() : ""
                                    };
                                   
                                    await _webSocketHandler.UserMessageAsync(empId, JsonSerializer.Serialize(progressNotification));

                                }
                            }
                        }
                    }
                    catch (Exception archiveEx)
                    {
                        _logger.LogError(archiveEx, "Error reading/extracting archive file: {FileName}. File might be corrupted or password-protected.", archiveFile.FileName);
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Could not extract '{archiveFile.FileName}'. File may be corrupted or password-protected.",
                            error = archiveEx.Message
                        });
                    }

                    // Database status update on first successful archive
                    if (isFirstArchiveProcess)
                    {
                        using (var conn = new SqlConnection(_connectionString))
                        {
                            await conn.OpenAsync();
                            var qry = "UPDATE TestCases SET status = @Status WHERE TestName = @TestName";
                            await conn.ExecuteAsync(qry, new { Status = "Y", TestName = TestName });
                            conn.Close();
                        }
                        isFirstArchiveProcess = false;
                        _logger.LogInformation("TestStatus updated to 'Y' in DB for TestName: {TestName}", TestName);
                    }
                }

                // Reset/Complete WebSocket message
                await _webSocketHandler.UserMessageAsync(empId, "");

                _logger.LogInformation("Successfully processed {ArchiveCount} archives. Extracted total {ImageCount} images for EmpID: {EmpId}", files.Count, extractedImagesCount, empId);

                return Ok(new
                {
                    success = true,
                    message = "Archive file(s) processed and images extracted successfully.",
                    extractedImagesCount = extractedImagesCount,
                    totalArchiveFiles = files.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during file upload processing for TestName: {TestName}", TestName);
                return StatusCode(500, new
                {
                    success = false,
                    message = "An unexpected error occurred while processing archive files.",
                    error = ex.Message
                });
            }
        }

    }
}

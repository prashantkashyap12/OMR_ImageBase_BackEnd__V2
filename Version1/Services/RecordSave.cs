using System.Collections.Generic;
using System.Linq;
using Dapper;
using DocumentFormat.OpenXml;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Syncfusion.EJ2.Linq;
using TesseractOCR.Renderers;
using Version1.Modal;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SQCScanner.Services
{
    public class RecordSave
    {


        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _env;
        private readonly RealtimeCSV_Rec _realTimeCSV;
        public RecordSave(IConfiguration configuration, IWebHostEnvironment env, RealtimeCSV_Rec realTimeCSV)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("dbc")!;
            _env = env;
            _realTimeCSV = realTimeCSV;
        }

        // Save Record into DB 
        public async Task<Dictionary<string, string>> RecordSaveVal(OmrResult respose, int templateId, string userName, bool IsSaveDb, string folderPath, string imagePath, string templateName, string userId)
        {
            Dictionary<string, string> records = new Dictionary<string, string>();
            userName = userName ?? "";
            var Query = "";
            dynamic res;
            dynamic result;
            string csvName= folderPath;

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    string tableName = $"Tem_{userId}_${templateId}$_{folderPath}";
                    string checkTableSql = @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE @TableName";
                    var exists = await connection.QueryFirstOrDefaultAsync(checkTableSql, new { TableName = tableName+"%" });
                    bool tableExists = exists != null;

                    // Insert record into Distnory. 
                    records.Add("LiveTime", DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt"));
                    records.Add("UserName", userName.ToString());
                    records.Add("Status", respose.Success.ToString());
                    var fields2 = respose.FieldResults.ToDictionary(fr => fr.Key, dl => dl.Value);
                    var fileName = "";
                    var sharePath = $"wwwroot/ScanResult/{userId}/{folderPath}/";
                    var imgName = "";
                    var pathIns = "";

                    if (respose.Success)
                    {
                        sharePath = Path.Combine(sharePath,"Success");
                        records.Add("Report", "Image is Scanned Successfully");             // true k case me auto add ni aata.
                        foreach (var kvp in fields2)
                        {
                            if (kvp.Key == "FileName")
                            {
                                imgName = kvp.Value;
                                pathIns = Path.Combine(sharePath, kvp.Value);
                                pathIns = pathIns.Replace("\\", "/");
                                records.Add(kvp.Key, pathIns);
                                fileName = pathIns;
                            }
                            else
                            {
                                records.Add(kvp.Key, kvp.Value); 
                            }
                        }
                    }
                    else
                    {
                        sharePath = Path.Combine(sharePath,"Reject");
                        bool injectImg = true;
                        foreach (var filds in fields2)
                        {
                            if(filds.Key== "FileName")
                            {
                                imgName = filds.Value;
                                pathIns = Path.Combine(sharePath,filds.Value);
                                pathIns = pathIns.Replace("\\", "/");
                                records.Add(filds.Key, pathIns);
                                fileName = pathIns;
                                injectImg = false;
                            }
                            else
                            {
                                records.Add(filds.Key, filds.Value);
                            }
                        }

                        // CSV Create 

                        //if (injectImg)
                        //{
                        //    string imgName1 = Path.GetFileName(imagePath);
                        //    imgName1 = Path.Combine("wFileManager", folderPath, imgName1);
                        //    records.TryAdd("FileName", imgName1);
                        //}
                        //_realTimeCSV.RealtimeCSV(userName, templateId, records, templateName, csvName);

                        //// save error msg only on remaing column.
                        //Query = @$"select column_name from INFORMATION_SCHEMA.columns where table_name = @TableName";
                        //var col = (await connection.QueryAsync<string>(Query, new { TableName = tableName })).Select(a => a).ToHashSet();
                        //foreach (var kvp in col)
                        //{
                        //    if (kvp == "Id" || kvp == "LiveTime" || kvp == "UserName" || kvp == "Status" || kvp == "FileName" || kvp == "Report")
                        //    {
                        //        continue;
                        //    }
                        //    else
                        //    {
                        //        records.Add(kvp, "error");
                        //    }
                        //}
                    }

                
                    if (tableExists)
                    {
                        // Does Record is Exist or Not.
                        // here we are just check is successFull Check img done.
                        string RecordExis1 = "";
                        string RecordExist2 = "";
                        //var checkSuss = $"wwwroot/ScanResult/ScannedImg/" + tableName + '/' + imgName;
                        //var checkfail = $"wwwroot/RejectImg/" + tableName + '/' + imgName;
                        //tableName = exists.TABLE_NAME;
                        RecordExis1 = $@"select * from [{exists?.TABLE_NAME?.ToString()}] where FileName LIKE '%{pathIns}'";
                        var isRes = connection.Query(RecordExis1).ToList();
                        if (isRes.Count>=1)
                        {
                            if (File.Exists(RecordExis1))
                            {
                                File.Delete(Path.Combine(_env.ContentRootPath, pathIns));

                            }
                        }
                        RecordExist2 = $"select * from [{exists?.TABLE_NAME?.ToString()}] where FileName LIKE '%{Path.Combine($"wwwroot/ScanResult/{userId}/{folderPath}/", "Reject", Path.GetFileName(pathIns))}'";
                        isRes = connection.Query(RecordExist2).ToList();
                        if (isRes.Count >= 1)
                        {
                            if (File.Exists(RecordExis1))
                            {
                                File.Delete(Path.Combine(_env.ContentRootPath, Path.Combine($"wwwroot/ScanResult/{userId}/{folderPath}/", "Reject", Path.GetFileName(pathIns))));
                            }
                        }

                        string existDirect = "";
                        foreach (var dataList in records)
                        {
                            string saveFile = "";
                            if(dataList.Key== "FileName")
                            {
                                saveFile = Path.Combine(_env.ContentRootPath, dataList.Value);
                                existDirect = Path.GetDirectoryName(saveFile);
                                if (!Directory.Exists(existDirect))
                                {
                                    Directory.CreateDirectory(existDirect);
                                }


                                // Save File into Directory
                                using (var ImgTemp = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                                using (var TempSet = new FileStream(saveFile, FileMode.Create, FileAccess.Write))
                                {
                                    await ImgTemp.CopyToAsync(TempSet);
                                }
                            }
                        }
                        records.TryAdd("QC_Status","");
                        if (isRes.Count >= 1) 
                        {
                            // Record Update _ Problem Will be = jab koi record(column ki rows) pahle se add ho gya hai or us record ko mene apne main scan response se hata diya hai wo update k case me hatega ni pahle waha record db se save rahega.
                            // Before save some record we will. (check FileName) 
                            var isFileCheck = respose.FieldResults.Where(x => x.Key == "FileName").ToList();
                            var isFileCheck2 = isFileCheck[0].Value;
                            var updateQuery = $@"UPDATE [{tableName}] SET {string.Join(", ", records.Select(kvp => $"[{kvp.Key}] = '{kvp.Value}'"))} WHERE WHERE RIGHT(FileName, CHARINDEX('/', REVERSE(FileName)) - 1) = '{fileName}'";
                            if (IsSaveDb)
                            {
                                result = await connection.ExecuteAsync(updateQuery);  
                            }
                            else
                            {
                                folderPath = Path.Combine("wFileManager/", folderPath, imgName);
                                pathIns = pathIns.Replace("\\", "/");
                                //records.Add("ServePath", folderPath);
                                records["FileName"] = folderPath;
                            }
                            res = new
                            {
                                res = records,
                                status = true
                            };
                        }
                        else
                        {
                            var insertQuery = $@"INSERT INTO [{exists?.TABLE_NAME?.ToString()}] ({string.Join(", ", records.Select(a => $"[{a.Key}]"))}) VALUES ({string.Join(", ", records.Select(c => "'" + c.Value + "'"))})";
                            result = connection.Query(insertQuery);
                            //string imgName1 = Path.GetFileName(imagePath);
                            //folderPath = Path.Combine("wFileManager/", folderPath, imgName1);
                            //records.Add("ServePath", folderPath);
                            //records["FileName"] = folderPath;
                            existDirect = Path.GetDirectoryName(existDirect);
                            string resulta = string.Join(",",records.Select(x => $"{x.Key}={x.Value}"));
                            _realTimeCSV.RealtimeCSV(records, csvName, existDirect);
                            res = new
                            {
                                res = records,
                                status = true
                            };
                        }
                    }
                    else
                    {
                        res = new
                        {
                            res = "Table is not Available",
                            status = false
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                res = new
                {
                    res = ex.Message,
                    status = false
                };
            }

            return records;
        }
    }
}

using System.Net.WebSockets;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using Syncfusion.EJ2.Notifications;
using TesseractOCR.Pix;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Newtonsoft.Json.Linq;
using SQCScanner.Services;

using static Version1.Controllers.OmrProcessingController;
using System.Net.Http.Headers;
using ZXing.Aztec.Internal;
using Newtonsoft.Json;
using System;
namespace SQCScanner.websoketManager
{
    public class WebSoketHandler
    {
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly OmrProcessingControlService _controlService;
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public WebSoketHandler(WebSocketConnectionManager connectionManager, OmrProcessingControlService controlService, HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
        {
            _connectionManager = connectionManager;
            _controlService = controlService;
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
        }

        // send Massage based userId sapcefic and // check if socket is open or not
        public async Task UserMessageAsync(string userId, string message)
        {
            var socket = _connectionManager.GetSocketByUserId(userId);

            if (socket == null)
            {
                Console.WriteLine($"No socket found for userId: {userId}");
                return;
            }

            if (socket.State != WebSocketState.Open)
            {
                Console.WriteLine($" Socket for {userId} is not open. State: {socket.State}");
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(message);
            try
            {
                if (message != "")
                {
                    Console.WriteLine($" Sending message to {userId}: {message}");
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    _connectionManager.RemoveSocket(userId);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to {userId}: {ex.Message}");
                _connectionManager.RemoveSocket(userId);
            }
        }

        // BroadCast Massage 
        public async Task BroadcastTestAsync(string message)
        {
            // to find connected Allsoket
            foreach (var socket in _connectionManager.GetAllSockets())
            {
                // those are ture and open
                if (socket.State == WebSocketState.Open)
                {
                    // Message 
                    var bytes = Encoding.UTF8.GetBytes(message);

                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }


        public async Task HandleControlMessage(string message, WebSocket webSocket)
        {
            var request = JObject.Parse(message);
            var action = request["action"]?.ToString()?.ToLower();
            bool success = true;
            string responseMessage = "";
            try
            {

                switch (action)
                {
                    case "pause":
                        _controlService.PauseProcessing();
                        responseMessage = "OMR Processing Paused Successfully";
                        Console.WriteLine(responseMessage);
                        break;

                    case "resume":
                        _controlService.ResumeProcessing();
                        responseMessage = "OMR Processing Resumed Successfully";
                        Console.WriteLine(responseMessage);
                        break;

                    case "stop":
                        _controlService.StopProcessing();
                        responseMessage = "OMR Processing Stopped Successfully";
                        Console.WriteLine(responseMessage);
                        break;

                    case "reset":
                        _controlService.ResetProcessing();
                        responseMessage = "OMR Processing Reset Successfully";
                        Console.WriteLine(responseMessage);
                        break;

                    case "process":
                        var model = new processOmr
                        {
                            folderPath = request["folderPath"]?.ToString() ?? "",
                            idTemp = request["idTemp"]?.Value<int>() ?? 0,
                            IsSaveDb = true,
                            failReScan = true,
                            Token = request["token"]?.ToString() ?? ""
                        };
                        try
                        {

                            var result = await ProcessOmrApi(model);
                            success = result;
                            responseMessage = result
                                ? "OMR API Called Successfully"
                                : "OMR Processing Failed";
                        }
                        catch (Exception ex)
                        {
                            responseMessage = ex.Message;
                        }
                        break;

                    default:
                        success = false;
                        responseMessage = $"Unknown WebSocket action: {action}";
                        Console.WriteLine(responseMessage);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket control message error: {ex.Message}");

                if (webSocket.State == WebSocketState.Open)
                {
                    var errorResponse = new
                    {
                        success = false,
                        message = ex.Message
                    };

                    string json = Newtonsoft.Json.JsonConvert
                        .SerializeObject(errorResponse);

                    byte[] bytes = Encoding.UTF8.GetBytes(json);

                    await webSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
                }
            }
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    var response = new
                    {
                        action = action,
                        success = success,
                        message = responseMessage
                    };

                    string json = JsonConvert.SerializeObject(response);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);

                    await webSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );

                    Console.WriteLine($"WebSocket response sent: {json}");
                }
                else
                {
                    Console.WriteLine(
                        $"Cannot send response. WebSocket state: {webSocket.State}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket response error: {ex.Message}");
            }
        }


        //public async Task<bool> ProcessOmrApi(processOmr model)
        //{
        //    try
        //    {

        //        var token = context.Request.Query["token"].ToString();
        //        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        //        var response = await _httpClient.PostAsJsonAsync("http://192.168.1.26:9900/api/OmrProcessing/process-omr", model);

        //        if (response.IsSuccessStatusCode)
        //        {
        //            return true;
        //        }
        //        var error = await response.Content.ReadAsStringAsync();

        //        Console.WriteLine($"OMR API Error: {error}");

        //        return false;
        //    }
        //    catch(Exception Ex)
        //    {
        //        Console.WriteLine($"OMR API Exception: {Ex.Message}");
        //        return false;
        //    }
        //}

        public async Task<bool> ProcessOmrApi(processOmr model)
        {
            try
            {
                //var context = _httpContextAccessor.HttpContext;

                //if (context == null)
                //{
                //    Console.WriteLine("HTTP Context is null.");
                //    return false;
                //}

                //var token = context.Request.Query["token"].ToString();

                //if (string.IsNullOrWhiteSpace(token))
                //{
                //    Console.WriteLine("Token not found in WebSocket query string.");
                //    return false;
                //}

                //Console.WriteLine($"Token received. Length: {token.Length}");

                //_httpClient.DefaultRequestHeaders.Remove("Authorization");

                //_httpClient.DefaultRequestHeaders.Authorization =
                //    new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.PostAsJsonAsync(
                    "http://192.168.1.26:9900/api/OmrProcessing/process-omr",
                    model);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("OMR API call successful.");
                    return true;
                }

                var error = await response.Content.ReadAsStringAsync();

                Console.WriteLine(
                    $"OMR API Status: {(int)response.StatusCode}");

                Console.WriteLine(
                    $"OMR API Error: {error}");

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"OMR API Exception: {ex.Message}");

                return false;
            }
        }
    }
}

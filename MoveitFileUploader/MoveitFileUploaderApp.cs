using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace MoveitFileUploaderLib
{
    public class MoveitFileUploaderApp
    {
        private string _folderPath;
        private string _username;
        private string _password;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MoveitFileUploaderApp> _logger;

        public MoveitFileUploaderApp(IHttpClientFactory httpClientFactory, ILogger<MoveitFileUploaderApp> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            try
            {
                Console.Write("Enter username: ");
                _username = Console.ReadLine();
                Console.Write("Enter password: ");
                _password = ReadPassword();

                if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
                {
                    _logger.LogError("Username and password must be provided.");
                    return;
                }

                // Login and get access token
                string accessToken = await GetAccessToken();

                // Ask for local folder path
                Console.Write("Enter local folder path: ");
                _folderPath = Console.ReadLine();

                // Adjust format of local folder path if needed
                _folderPath = _folderPath.Replace("\\", "\\\\");

                if (!Directory.Exists(_folderPath))
                {
                    _logger.LogError($"Directory '{_folderPath}' does not exist.");
                    return;
                }

                // Sync folders initially
                await SyncFoldersAsync(accessToken);

                // List and print local files initially
                ListAndPrintLocalFiles();

                // Display uploaded files initially
                await DisplayUploadedFilesAsync(accessToken);

                // Set up FileSystemWatcher
                FileSystemWatcher watcher = new FileSystemWatcher(_folderPath)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    Filter = "*.*",
                    EnableRaisingEvents = true
                };

                watcher.Created += async (sender, e) => await OnFileCreated(e.FullPath);
                watcher.Deleted += async (sender, e) => await OnFileDeleted(e.FullPath);

                _logger.LogInformation($"Monitoring {_folderPath}. Press [enter] to exit.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in RunAsync method.");
            }
        }

        private void ListAndPrintLocalFiles()
        {
            _logger.LogInformation("Listing files in the local folder:");
            var files = Directory.GetFiles(_folderPath);
            Console.WriteLine("Local files:");
            foreach (var file in files)
            {
                Console.WriteLine(Path.GetFileName(file));
            }
        }

        private async Task<string> GetAccessToken()
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("username", _username),
                    new KeyValuePair<string, string>("password", _password)
                });

                var response = await client.PostAsync("https://testserver.moveitcloud.com/api/v1/token", content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseString);
                return jsonResponse.access_token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve access token.");
                throw;
            }
        }

        private async Task SyncFoldersAsync(string accessToken)
        {
            try
            {
                _logger.LogInformation("Syncing folders...");

                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.GetAsync("https://testserver.moveitcloud.com/api/v1/files");
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseString);
                var items = jsonResponse.items;

                foreach (var item in items)
                {
                    string filePath = Path.Combine(_folderPath, item.name.ToString());
                    string fileId = item.id.ToString();

                    if (!File.Exists(filePath))
                    {
                        _logger.LogInformation($"Downloading {item.name}...");
                        var fileResponse = await client.GetAsync($"https://testserver.moveitcloud.com/api/v1/files/{fileId}/download");
                        fileResponse.EnsureSuccessStatusCode();

                        var fileBytes = await fileResponse.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(filePath, fileBytes);
                        _logger.LogInformation($"{item.name} downloaded.");
                    }
                }

                _logger.LogInformation("Folders synced.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync folders.");
                throw;
            }
        }

        private async Task DisplayUploadedFilesAsync(string accessToken)
        {
            try
            {
                _logger.LogInformation("Displaying uploaded files...");

                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.GetAsync("https://testserver.moveitcloud.com/api/v1/files");
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseString);
                var items = jsonResponse.items;

                Console.WriteLine("Uploaded files:");
                foreach (var item in items)
                {
                    Console.WriteLine(item.name.ToString());
                }

                _logger.LogInformation("Files displayed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to display uploaded files.");
                throw;
            }
        }

        private async Task OnFileCreated(string filePath)
        {
            _logger.LogInformation($"File {filePath} has been added.");
            try
            {
                string token = await GetAccessToken();
                string homeFolderID = await GetHomeFolder(token);
                await UploadFileToMoveitTransfer(filePath, token, homeFolderID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while uploading the file.");
            }
        }

        private async Task OnFileDeleted(string filePath)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                string accessToken = await GetAccessToken();
                await DeleteFileFromCloud(fileName, accessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while deleting {Path.GetFileName(filePath)} from cloud.");
            }
        }

        private async Task<string> GetHomeFolder(string accessToken)
        {
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync("https://testserver.moveitcloud.com/api/v1/users/self");
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var jsonResponse = JObject.Parse(responseString);

            _logger.LogInformation($"Received user info response: {responseString}");

            // Ensure that the response contains the expected structure
            if (jsonResponse["homeFolderID"] == null)
            {
                _logger.LogError("homeFolderID is null in the response.");
                throw new InvalidOperationException("The homeFolderID field is missing in the response.");
            }

            return jsonResponse["homeFolderID"].ToString();
        }

        private async Task UploadFileToMoveitTransfer(string filePath, string token, string homeFolderID)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var form = new MultipartFormDataContent();
                byte[] fileBytes = File.ReadAllBytes(filePath);
                form.Add(new ByteArrayContent(fileBytes, 0, fileBytes.Length), "file", Path.GetFileName(filePath));

                var response = await client.PostAsync($"https://testserver.moveitcloud.com/api/v1/folders/{homeFolderID}/files", form);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation($"File {filePath} uploaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to upload file {filePath}.");
                throw;
            }
        }

        private async Task DeleteFileFromCloud(string filePath, string accessToken)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var fileName = Path.GetFileName(filePath);
                var response = await client.GetAsync("https://testserver.moveitcloud.com/api/v1/files");
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseString);
                var items = jsonResponse.items;

                string fileId = null;
                foreach (var item in items)
                {
                    if (item.name.ToString() == fileName)
                    {
                        fileId = item.id.ToString();
                        break;
                    }
                }

                if (fileId != null)
                {
                    var deleteResponse = await client.DeleteAsync($"https://testserver.moveitcloud.com/api/v1/files/{fileId}");
                    deleteResponse.EnsureSuccessStatusCode();
                    _logger.LogInformation($"File {fileName} deleted from cloud.");
                }
                else
                {
                    _logger.LogWarning($"File {fileName} not found in cloud.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete file {Path.GetFileName(filePath)} from cloud.");
            }
        }

        private string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                // Handle backspace
                if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password[0..^1];
                    Console.Write("\b \b"); // Erase the last character from console
                }
                // Ignore any key out of range.
                else if (!char.IsControl(key.KeyChar))
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                // Exit if Enter key is pressed.
            } while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();
            return password;
        }
    }
}

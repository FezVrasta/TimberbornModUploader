using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace ModUploader
{
    // Adds a file to a mod's page on mod.io
    internal static class ModIo
    {
        public static string Upload(string game, string mod, string zip, string version, string note, string token)
        {
            using (var client = new HttpClient { Timeout = System.TimeSpan.FromMinutes(10) })
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using (var form = new MultipartFormDataContent())
                using (var file = File.OpenRead(zip))
                {
                    form.Add(new StreamContent(file), "filedata", Path.GetFileName(zip));
                    form.Add(new StringContent(version), "version");
                    form.Add(new StringContent(note), "changelog");
                    form.Add(new StringContent("true"), "active");
                    var response = client.PostAsync($"https://g-{game}.modapi.io/v1/games/{game}/mods/{mod}/files", form).GetAwaiter().GetResult();
                    string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if ((int)response.StatusCode == 201)
                        return "modio ok " + Regex.Match(body, "\"id\": *([0-9]+)").Groups[1].Value;
                    return $"modio failed {(int)response.StatusCode} {Regex.Match(body, "\"message\": *\"([^\"]*)\"").Groups[1].Value}";
                }
            }
        }
    }
}

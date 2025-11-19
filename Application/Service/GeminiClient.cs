using Application.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Application.Service
{
    public class GeminiClient : IGeminiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GeminiClient(HttpClient httpClient, IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<object> CallGemini(string prompt)
        {
            var apiKey = _configuration["GEMINI_API_Key:Key"];
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Gemini API Error ({response.StatusCode}): {errorMsg}");
                }

                var responseString = await response.Content.ReadAsStringAsync();

                var geminiResponse = JsonNode.Parse(responseString);
                var candidates = geminiResponse?["candidates"];
                var text = candidates?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();

                return text;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to call Gemini: {ex.Message}");
            }
        }
    }
}

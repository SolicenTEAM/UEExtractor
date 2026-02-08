using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Solicen.Translator
{
    #region Модели для OpenRouter API
    public class OpenRouterMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

    public class OpenRouterRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("messages")]
        public List<OpenRouterMessage> Messages { get; set; }
    }

    public class OpenRouterChoice
    {
        [JsonProperty("message")]
        public OpenRouterMessage Message { get; set; }
    }

    public class OpenRouterResponse
    {
        [JsonProperty("choices")]
        public List<OpenRouterChoice> Choices { get; set; }
    }
    #endregion

    /// <summary>
    /// Простой и легковесный клиент для взаимодействия с API OpenRouter.
    /// </summary>
    internal class OpenRouterApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public string LastError { get; private set; }

        public OpenRouterApiClient(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
                Timeout = TimeSpan.FromSeconds(500)
            };

            // Настраиваем заголовки, которые OpenRouter ожидает
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", ""); // Рекомендуемый заголовок
            _httpClient.DefaultRequestHeaders.Add("X-Title", "Kismet Editor"); // Рекомендуемый заголовок
        }

        public async Task<OpenRouterResponse> ChatAsync(OpenRouterRequest request)
        {
            try
            {
                // Сериализуем наш объект запроса в JSON. Null-поля будут игнорироваться.
                var jsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                var jsonContent = JsonConvert.SerializeObject(request, jsonSettings);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("chat/completions", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<OpenRouterResponse>(responseJson);
                }
                else
                {
                    // Если произошла ошибка, сохраняем ее для диагностики
                    var errorContent = await response.Content.ReadAsStringAsync();
                    LastError = $"{(int)response.StatusCode} {response.ReasonPhrase}: {errorContent}";
                    return null;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }
    }
}
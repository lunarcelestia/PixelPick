using System;
using System.Net.Http;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace PixelPick
{
    public class Program
    {
        static string prompt;
        static string apiKey;
        static string telegramBotToken;
        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
{
    apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    telegramBotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
    
    if (string.IsNullOrEmpty(telegramBotToken))
    {
        Console.WriteLine("ERROR: TELEGRAM_BOT_TOKEN not found!");
        return;
    }

    Console.WriteLine($"TELEGRAM_BOT_TOKEN: {telegramBotToken?.Substring(0, Math.Min(10, telegramBotToken.Length))}...");

    botClient = new TelegramBotClient(telegramBotToken);
    var bot = new PixelPickBot(botClient);
    botClient.StartReceiving(Update, Error);
    _ = bot.SendDailyUpdates();

    Console.WriteLine("Бот запущен!");


    while (true)
    {
        await Task.Delay(1000);
    }
}

        async static Task Update(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                Console.WriteLine("Операция отменена.");
                return;
            }

            var message = update.Message;

            if (message != null && message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
            {
                if (message.Text.StartsWith("/start"))
                {
                    string welcomeMessage = "привет! я PixelPick, помогу найти компьютерную игру именно для тебя!\n" +
                        "────  ────\n" +
                                            "просто напиши мне критерии, которые для тебя важны в новой игре, и я попробую найти подходящие игры.";
                    await botClient.SendTextMessageAsync(message.Chat.Id, welcomeMessage, cancellationToken: token);
                    return; 
                }
                else if (message.Text.StartsWith("/help"))
                {
                    string helpMessage = "я PixelPick, бот для поиска компьютерных игр\n" +
                                         "╰┈➤ты можешь запустить меня, использовав команду /start.\n" +
                                         "╰┈➤теперь, чтобы получить подборку игр, просто напиши мне критерии, которые для тебя важны в игре (например, 'игры, похожие на...', 'RPG с открытым миром').\n" +
                                         "╰┈➤я постараюсь найти подходящие игры, используя эти критерии.\n" +
                                         "чем более точно ты опишешь, во что бы ты хотел поиграть, тем с большей вероятностью я подберу для тебя то, что нужно♡";
                    await botClient.SendTextMessageAsync(message.Chat.Id, helpMessage, cancellationToken: token);
                    return; 
                }
                else
                {
                    prompt = message.Text;

                    try
                    {
                        string completedText = await GetOpenAICompletion(prompt);
                        await botClient.SendTextMessageAsync(message.Chat.Id, completedText, cancellationToken: token);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при обработке запроса: {ex.Message}");
                        await botClient.SendTextMessageAsync(message.Chat.Id, "сейчас мой мозг не работает... мне нужно время...", cancellationToken: token);
                    }

                    return;
                }
            }
        }

        static async Task<string> GetOpenAICompletion(string prompt)
        {
            string proxyApiKey = "sk-MuAF57ghwdMALYxY4E0kICX1G8UxzcRP"; 
            string requestUrl = "https://api.proxyapi.ru/openai/v1/chat/completions"; 
            string model = "gpt-4-turbo-preview"; 

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {proxyApiKey}"); 

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
            new { role = "user", content = prompt }
        },
                temperature = 0.5, 
                max_tokens = 1024  
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync(requestUrl, content);
                response.EnsureSuccessStatusCode(); 

                var responseString = await response.Content.ReadAsStringAsync();
                dynamic responseObject = JsonConvert.DeserializeObject(responseString);
                if (responseObject.choices != null && responseObject.choices.Count > 0)
                {
                    return responseObject.choices[0].message.content;
                }
                else
                {
                    Console.WriteLine("No choices returned from OpenAI.");
                    return "Error: No response from OpenAI.";
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP Request Error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
            catch (JsonSerializationException ex)
            {
                Console.WriteLine($"JSON Serialization Error: {ex.Message}");
                return "Error: Problem serializing the request or deserializing the response.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Generic Error: {ex.Message}");
                return "Error: An unexpected error occurred.";
            }
            try
            {
                HttpResponseMessage response = await httpClient.PostAsync(requestUrl, content);
                response.EnsureSuccessStatusCode(); 

                string responseJson = await response.Content.ReadAsStringAsync();
                dynamic responseObject = JsonConvert.DeserializeObject(responseJson);
                return responseObject.choices[0].text;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Ошибка при запросе к OpenAI API: {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Ошибка при десериализации ответа OpenAI: {ex.Message}");
                throw;
            }
        }

        private static Task Error(ITelegramBotClient arg1, Exception arg2, CancellationToken arg3)
        {
            Console.WriteLine($"Ошибка Telegram Bot API: {arg2.Message}");
            return Task.CompletedTask; 
        }
    }
}



using System;
using System.Net.Http;
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
using DotNetEnv;

namespace PixelPick
{
    public class Program
    {
        private static List<UserSubscription> subscriptions = new List<UserSubscription>();
        private static ITelegramBotClient botClient;

        static string prompt;
        static string apiKey;
        static string telegramBotToken;
        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                Console.WriteLine($"Необработанное исключение: {((Exception)eventArgs.ExceptionObject).Message}");
                Console.WriteLine(((Exception)eventArgs.ExceptionObject).StackTrace);
                Environment.Exit(1);
            };

            DotNetEnv.Env.Load("C:\\Baza\\PixelPick\\PixelPick\\.env");
            Console.WriteLine("Файл .env загружен.");

            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            telegramBotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");

            Console.WriteLine($"TELEGRAM_BOT_TOKEN: {telegramBotToken}");

            botClient = new TelegramBotClient(telegramBotToken);
            var bot = new PixelPickBot(botClient);

            // Запуск получения обновлений в фоновом режиме
            botClient.StartReceiving(Update, Error);

            // Запуск метода для отправки ежедневных обновлений
            _ = bot.SendDailyUpdates();

            Console.WriteLine("Бот запущен!");
            Console.ReadLine(); 
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
                
                Console.WriteLine($"Received message: {message.Text} from {message.From.Id}");

                if (message.Text.StartsWith("/start"))
                {
                    string welcomeMessage = "Привет! Я PixelPick, помогу найти компьютерную игру именно для тебя!\n" +
                        "▬▬ι═══════ﺤ\n" +
                        "Просто напиши мне критерии, которые для тебя важны в новой игре, и я попробую найти подходящие игры.";
                    await botClient.SendTextMessageAsync(message.Chat.Id, welcomeMessage, cancellationToken: token);
                    return; 
                }
                else if (message.Text.StartsWith("/help"))
                {
                    string helpMessage = "Я PixelPick, бот для поиска компьютерных игр.\n" +
                                         "⌖ Ты можешь запустить меня, использовав команду /start.\n" +
                                         "⌖ Теперь, чтобы получить подборку игр, просто напиши мне критерии, которые для тебя важны в игре (например, 'игры, похожие на...', 'RPG с открытым миром').\n" +
                                         "⌖ Я постараюсь найти подходящие игры, используя эти критерии.\n" +
                                         "Чем более точно ты опишешь, во что бы ты хотел поиграть, тем с большей вероятностью я подберу для тебя то, что нужно. \n" +
                                         "▬▬ι═══════ﺤ \n" +
                                         "⌖ Также, если ты хочешь получать подборки игр, основанные на твоих предпочтениях, ты можешь подписаться на рассылку подобранных для тебя игр.\n" + 
                                         "Для этого напиши команду /follow";
                    await botClient.SendTextMessageAsync(message.Chat.Id, helpMessage, cancellationToken: token);
                    return; 
                }
                else if (message.Text.StartsWith("/follow"))
                {
                    await FollowUser(message.From.Id);
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Теперь ты подписан на рассылку подобранных для тебя игр.\n" +
                        "Рассылка происходит раз в 3 дня. \n" +
                        "▬▬ι═══════ﺤ\n" +
                        "Если ты хочешь отписаться от рассылки, воспользуйся командой /unfollow");
                    return;
                }
                else if (message.Text.StartsWith("/unfollow"))
                {
                    await UnfollowUser(message.From.Id);
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Вы отписаны от рассылки.");
                    return; 
                }
                string prompt = message.Text;

                try
                {
                    string completedText = await GetOpenAICompletion(prompt);
                    await botClient.SendTextMessageAsync(message.Chat.Id, completedText, cancellationToken: token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при обработке запроса: {ex.Message}");
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Сейчас мой мозг не работает... мне нужно время...", cancellationToken: token);
                }
            }
        }

        private static async Task FollowUser(long userId)
        {
            if (!subscriptions.Any(s => s.UserId == userId))
            {
                subscriptions.Add(new UserSubscription { UserId = userId, IsSubscribed = true });
            }
        }

        private static async Task UnfollowUser(long userId)
        {
            var subscription = subscriptions.FirstOrDefault(s => s.UserId == userId);
            if (subscription != null)
            {
                subscriptions.Remove(subscription);
            }
        }


        private async Task<List<string>> GetRecommendedGames(List<string> criteria)
        {
            List<string> recommendedGames = new List<string>();

            var url = "https://store.steampowered.com/api/featured";
            var response = await httpClient.GetStringAsync(url);
            var jsonResponse = JsonConvert.DeserializeObject<dynamic>(response);

            // Обработка данных о акциях
            foreach (var game in jsonResponse["featured_apps"])
            {
                string gameName = game["name"];
                string discount = game["discount_percent"].ToString();

                // Фильтрация по критериям
                foreach (var criterion in criteria)
                {
                    if (gameName.ToLower().Contains(criterion.ToLower()) || discount != "0") 
                    {
                        recommendedGames.Add($"{gameName} - Скидка: {discount}%");
                        break;
                    }
                }
            }

            return recommendedGames;
        }


        static async Task<string> GetOpenAICompletion(string prompt)
        {
            string proxyApiKey = Environment.GetEnvironmentVariable("PROXY_API_KEY");
            if (string.IsNullOrEmpty(proxyApiKey))
            {
                Console.WriteLine("Необходимо задать ключ ProxyAPI в переменной окружения PROXY_API_KEY");
                return "Error: ProxyAPI key is not set.";
            }

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
                response.EnsureSuccessStatusCode(); // Проверка на успешный код ответа

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

        public class PixelPickBot
        {
            private readonly ITelegramBotClient botClient;

            public PixelPickBot(ITelegramBotClient bot)
            {
                botClient = bot;
            }

            private async Task<string> GetGamesFromSteam(List<string> interests)
            {
                var url = $"https://api.steampowered.com/ISteamApps/GetAppList/v2/?key={Environment.GetEnvironmentVariable("STEAM_API_KEY")}";

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetStringAsync(url);
                    dynamic data = JsonConvert.DeserializeObject(response);

                    StringBuilder formattedMessage = new StringBuilder();
                    formattedMessage.AppendLine("Привет! Вот игры, которые возможно тебе понравятся:");

                    int count = 0;
                    foreach (var app in data.applist.apps)
                    {
                        if (count >= 10) break;

                        string gameName = app.name.ToString();
                        formattedMessage.AppendLine($"⌖ {gameName}");
                        count++;
                    }

                    return formattedMessage.ToString();
                }
            }

            public async Task SendDailyUpdates()
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromDays(3));

                    foreach (var subscription in subscriptions)
                    {
                        if (subscription.IsSubscribed)
                        {
                            try
                            {
                                var games = await GetGamesFromSteam(subscription.Interests);

                                Console.WriteLine($"Игры для пользователя {subscription.UserId}: {games}");

                                if (!string.IsNullOrEmpty(games))
                                {
                                    if (games.Length > 4096)
                                    {
                                        games = games.Substring(0, 4096);
                                    }

                                    Console.WriteLine($"Отправляем сообщение пользователю {subscription.UserId}: {games}");

                                    await botClient.SendTextMessageAsync(subscription.UserId, games);

                                    Console.WriteLine($"Отправлено сообщение пользователю: {subscription.UserId}");
                                }
                                else
                                {
                                    Console.WriteLine($"Нет подходящих игр для пользователя: {subscription.UserId}.");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка при отправке сообщения пользователю {subscription.UserId}: {ex.Message}");
                                Console.WriteLine("Stack Trace: " + ex.StackTrace);
                            }
                        }
                    }
                }
            }

        }

        
    }
}

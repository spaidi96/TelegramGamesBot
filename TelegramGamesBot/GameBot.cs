using System;
using System.Collections.Generic;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
//using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot.Polling;

namespace TelegramGameBot;

public class GameBot
{
    TelegramBotClient botClient = new TelegramBotClient("6044056155:AAGgUVDedV767TLAQ2Ez5fz8E8xyb0UTKSw");
    public CancellationToken cancellationToken = new CancellationToken();
    ReceiverOptions receiverOptions = new ReceiverOptions { AllowedUpdates = { } };
    static HttpClient httpClient = new HttpClient();
    string texthelp =
        $"Ось список команд, які я вмію виконувати:\n\n" +
        $"❕ /gameInfo (назва гри) - дозволить тобі дізнатися корисну інформацію про гру 📖\n" +
        $"❕ /gameScreenshot (назва гри) - дозволить тобі переглянути красиві скріншоти з гри 📷\n" +
        $"❕ /gameStore (назва гри) - дозволить тобі отримати посилання на магазини, де ти зможеш купити гру 🛒\n" +
        $"❕ /sameSeries (назва гри) - дозволить тобі отримати інформацію про ігри, які є частиною однієї серії серії 👬\n" +
        $"❕ /popularGames (початковий рік, наприклад 2022) (кінцевий рік, наприклад 2023) - дозволить отримати список найпопулярніших ігор за обраний тобою рік 🤩\n" +
        $"❕ /upComingGames - дозволить отримати список найочікуваніших ігор, які мають вийти в скорому часі 🎉";

    public async Task Start()
    {
        botClient.StartReceiving(HandlerUpdateAsync, HandlerError, receiverOptions, cancellationToken);
        var botMe = await botClient.GetMeAsync();
        Console.WriteLine($"Bot {botMe.Username} started");
        Console.ReadKey();
    }

    public Task HandlerError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Помилка в телеграм бот API: {apiRequestException.ErrorCode} {apiRequestException.Message}",
            _ => exception.ToString()
        };
        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }

    public async Task HandlerUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update?.Message?.Text != null)
        { 
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(update));
            await HandlerMessageAsync(botClient, update.Message);
        }
    }

    public async Task HandlerMessageAsync(ITelegramBotClient botClient, Message message)
    {
        if (message.Text.ToLower() == "/start")
        {
            var text =
                $"Привіт-привіт, {message.From.FirstName}!\n\n" +
                $"Давай почнемо знайомство зі світом комп'ютерних ігор! 🎮\n" +
                $"Щоб дізнатися список команд, які я вмію виконувати пропиши /help";
            await botClient.SendTextMessageAsync(message.Chat.Id, text);
            ReplyKeyboardMarkup replyKeyboardMarkup = new
            (
                new[]
                {
                    new KeyboardButton[] { "/help" }
                }
            )
            {
                ResizeKeyboard = true
            };
            
            await botClient.SendTextMessageAsync(message.Chat.Id, texthelp, replyMarkup: replyKeyboardMarkup);
        }
        
        else if (message.Text.ToLower() == "/help")
        {
            
            await botClient.SendTextMessageAsync(message.Chat.Id, texthelp);
        }
        else if (message.Text == "/upComingGames")
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response =
                        await client.GetAsync("https://game-bot-api.azurewebsites.net/UpcomingGames");
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync();

                    JObject data = JObject.Parse(responseBody);
                    JArray games = (JArray)data["results"];
                    await botClient.SendTextMessageAsync(message.Chat.Id, "⏰ Ось список найочікуваніших ігор, які мають вийти в скорому часі");
                    foreach (var game in games)
                    {
                        string name = game["name"].ToString();
                        string released = game["released"].ToString();
                        string screenshot = game["background_image"].ToString();
                        var text = 
                                   $"🌿 Назва: {name}\n" +
                                   $"📅 Released: {released}\n";
                        await botClient.SendTextMessageAsync(message.Chat.Id, text);
                        await botClient.SendPhotoAsync(message.Chat.Id, InputFile.FromUri(screenshot));
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Помилка при виконанні запиту до API: {ex.Message}");
                }
            }
        }
        else if (message.Text != null && message.Text.StartsWith("/gameInfo"))
        {
           
             string gameName = message.Text.Substring("/gameInfo".Length).Trim();
             gameName = gameName.Replace(" ", "-");
             string apiUrl = $"https://game-bot-api.azurewebsites.net/Games?id={gameName}";
             if (string.IsNullOrWhiteSpace(gameName))
             {
                 Console.WriteLine("Назва гри не може бути порожньою.");
                 return;
             }
             using (var client = new WebClient())
             {
                 try
                 {
                     string response = client.DownloadString(apiUrl);
                     JObject data = JObject.Parse(response);
                     if (data != null)
                     {
                         string name = data["name"].ToString();
                         JArray developers = data["developers"] as JArray;
                         JArray genres = data["genres"] as JArray;
                         string description = data["description"].ToString();
                         string metacritic = data["metacritic"].ToString();
                         string released = data["released"].ToString();
                         string backgroundImage = data["background_image"].ToString();
                         description = StripHTMLTags(description);
                         string gameInfo = $"Інформація про гру:\n\n" +
                                           $"🌿 Назва: {name}\n" +
                                           $"🔧 Розробники:\n";
                         foreach (JObject developer in developers)
                         {
                             string developerName = developer["name"].ToString();
                             gameInfo += $"- {developerName}\n";
                         }
                         gameInfo += "\n🎮 Жанри:\n";
                         foreach (JObject genre in genres)
                         {
                             string genreName = genre["name"].ToString();
                             gameInfo += $"- {genreName}\n";
                         }

                         gameInfo += $"\n 📝 Опис: {description}\n" +
                                     $" 🏆 Metacritic: {metacritic}\n" +
                                     $"📅 Дата виходу: {released}\n";
                         await botClient.SendTextMessageAsync(message.Chat.Id, gameInfo);
                         await botClient.SendPhotoAsync(message.Chat.Id, InputFile.FromUri(backgroundImage));
                     }
                     else
                     {
                         await botClient.SendTextMessageAsync(message.Chat.Id, "Помилка!😥 Можливо, ви неправильно ввели назву гри, спробуйте знову");
                         Console.WriteLine("Немає даних для цієї гри.");
                     }
                 }
                 catch (WebException ex)
                 {
                     await botClient.SendTextMessageAsync(message.Chat.Id, "Помилка!😥 Можливо, ви неправильно ввели назву гри, спробуйте знову");
                     Console.WriteLine($"Помилка під час отримання інформації про гру: {ex.Message}");
                 }
             }
        }
        
        else if (message.Text != null && message.Text.StartsWith("/gameScreenshot"))
        {
            string gameName = message.Text.Substring("/gameScreenshot".Length).Trim();
            gameName = gameName.Replace(" ", "-");
            string apiUrl = $"https://game-bot-api.azurewebsites.net/GameScreenshot?game_pk={gameName}";

            using (var client = new WebClient())
            {
                try
                {
                    string response = client.DownloadString(apiUrl);
                    JObject data = JObject.Parse(response);

                    if (data != null)
                    {
                        JArray results = data["results"] as JArray;

                        if (results != null && results.Count > 0)
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "📷 Скріншоти з гри:");

                            foreach (JObject screenshot in results)
                            {
                                string imageUrl = screenshot["image"].ToString();
                                await botClient.SendPhotoAsync(message.Chat.Id, InputFile.FromUri(imageUrl));
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Немає доступних скріншотів для цієї гри.");
                    }
                }
                catch (WebException ex)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Помилка!😥 Можливо, ви неправильно ввели назву гри, спробуйте знову");
                    Console.WriteLine($"Помилка під час отримання скріншотів гри: {ex.Message}");
                }
            }
        }
        
        else if (message.Text != null && message.Text.StartsWith("/gameStore"))
        {
            string gameName = message.Text.Substring("/gameStore".Length).Trim();
            gameName = gameName.Replace(" ", "-");
            string apiUrl = $"https://game-bot-api.azurewebsites.net/gameStore?game_pk={gameName}";
            using (var client = new WebClient())
            {
                try
                {
                    string response = client.DownloadString(apiUrl);
                    JObject data = JObject.Parse(response);

                    if (data != null)
                    {
                        JArray results = data["results"] as JArray;
                        await botClient.SendTextMessageAsync(message.Chat.Id, "🛒 Тримай посилання на магазини:");
                        foreach (JObject result in results)
                        {
                            string url = result["url"].ToString();
                            await botClient.SendTextMessageAsync(message.Chat.Id, url);
                        }
                    }
                }
                catch (WebException ex)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Помилка!😥 Можливо, ви неправильно ввели назву гри, спробуйте знову");
                    Console.WriteLine($"Помилка під час отримання інформації про гру: {ex.Message}");
                }
            }
        }
        else if (message.Text != null && message.Text.StartsWith("/sameSeries"))
        {
            string gameName = message.Text.Substring("/sameSeries".Length).Trim();
            gameName = gameName.Replace(" ", "-");
            string apiUrl = $"https://game-bot-api.azurewebsites.net/SameSeriesGames?game_pk={gameName}";
            using (var client = new WebClient())
            {
                try
                {
                    string response = client.DownloadString(apiUrl);
                    JObject data = JObject.Parse(response);

                    if (data != null)
                    {
                        JArray results = data["results"] as JArray;
                        await botClient.SendTextMessageAsync(message.Chat.Id, "🎮🕹 Інші ігри з тієї ж серії:");
                        foreach (JObject result in results)
                        {
                            string name = result["name"].ToString();
                            string released = result["released"]?.ToString();
                            string backgroundImage = result["background_image"].ToString();
                            await botClient.SendTextMessageAsync(message.Chat.Id,
                                $"🌿 Назва: {name}\n" +
                                $"📅 Дата виходу: {released ?? "Невідома"}\n");
                            await botClient.SendPhotoAsync(message.Chat.Id, InputFile.FromUri(backgroundImage));
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Помилка!😥 Можливо, ви неправильно ввели назву гри, спробуйте знову");
                        
                    }
                }
                catch (WebException ex)
                {
                    Console.WriteLine($"Помилка під час отримання інформації про ігри: {ex.Message}");
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Помилка!😥 Можливо, ви неправильно ввели назву гри, спробуйте знову");
                }
            }
        }
        else if (message.Text != null && message.Text.StartsWith("/popularGames"))
        {
            string input = message.Text.Substring("/popularGames".Length).Trim();
            string[] years = input.Split(' ');

            if (years.Length != 2)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id,
                    "😥 Помилка! Введіть коректні значення для першого року та другого року.");
                await botClient.SendStickerAsync(message.Chat.Id, InputFile.FromUri(
                    "https://tlgrm.eu/_/stickers/ccd/a8d/ccda8d5d-d492-4393-8bb7-e33f77c24907/192/12.webp"));
                return;
            }
            int firstYear = int.Parse(years[0]);
            int secondYear = int.Parse(years[1]);
            int yearDifference = Math.Abs(secondYear - firstYear);
            if (yearDifference > 1)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id,
                    "😥 Помилка! Потрібно ввести початок року та кінець, наприклад '2022 2023'.");
                return;
            }

            string apiUrl =
                $"https://game-bot-api.azurewebsites.net/MostPopularGames?first_date={firstYear}&second_date={secondYear}";

            using (var client = new WebClient())
            {
                try
                {
                    string response = client.DownloadString(apiUrl);
                    JObject data = JObject.Parse(response);

                    if (data != null)
                    {
                        JArray results = data["results"] as JArray;
                        await botClient.SendTextMessageAsync(message.Chat.Id,
                            $"Найпопулярніші ігри від {firstYear} до {secondYear} :");
                        foreach (JObject result in results)
                        {
                            string name = result["name"].ToString();
                            string released = result["released"].ToString();
                            string rating = result["rating"].ToString();
                            string ratingTop = result["rating_top"].ToString();
                            string backgroundImage = result["background_image"].ToString();
                            await botClient.SendTextMessageAsync(message.Chat.Id,
                                $"🌿 Назва: {name}\n" +
                                $"📅 Дата виходу: {released}\n" +
                                $"🏆 Рейтинг: {rating}/{ratingTop}\n");
                            await botClient.SendPhotoAsync(message.Chat.Id, InputFile.FromUri(backgroundImage));
                        }
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id,
                            "Помилка!😥 Немає даних про найпопулярніші ігри за цей період.");
                        
                    }
                }
                catch (WebException ex)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id,
                        "Помилка!😥 Немає даних про найпопулярніші ігри за цей період.");
                    Console.WriteLine($"Помилка під час отримання інформації про ігри: {ex.Message}");
                }
            }
        }
        else if (message.Text != null && message.Text.StartsWith("/addWishList"))
        {
            try
            {
                string gameName = message.Text.Substring("/addWishList".Length).Trim();
                
                var gameData = new
                {
                    name = gameName,
                    //description = gameDescription
                };
                string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(gameData);
    
                string apiUrl = "https://game-bot-api.azurewebsites.net/WishList";
    
                using (var client = new HttpClient())
                {
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(apiUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        botClient.SendTextMessageAsync(message.Chat.Id, "Інформація про гру була успішно записана.");
                        //Console.WriteLine("Інформація про гру була успішно записана.");
                    }
                    else
                    {
                        botClient.SendTextMessageAsync(message.Chat.Id, "Помилка при відправці POST-запиту до API.");
                       // Console.WriteLine("Помилка при відправці POST-запиту до API.");
                    }
                }
            }
            catch (Exception ex)
            {
                botClient.SendTextMessageAsync(message.Chat.Id, "Помилка при відправці POST-запиту до API.");
                Console.WriteLine($"Сталася помилка: {ex.Message}");
            }
        }
        else
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "На жаль, я не розумію, що ви написали😭");
        }
    }
    
    static string StripHTMLTags(string input)
    {
        return Regex.Replace(input, "<.*?>", "");
    }
    
}

    

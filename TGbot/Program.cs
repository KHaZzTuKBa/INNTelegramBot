using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
using TGbot.Models;

namespace Program
{
    partial class Solution
    {
        private static ITelegramBotClient _botClient;
        private static ReceiverOptions _receiverOptions;
        private static string _lastAction = "";

        static async Task Main()
        {
            _botClient = new TelegramBotClient(new StreamReader("botToken.txt").ReadLine());
            _receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[]
                {
                    UpdateType.Message,
                },
                ThrowPendingUpdates = true,
            };

            using var cts = new CancellationTokenSource();

            _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token);

            await Task.Delay(-1);
        }

        private static async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message != null)
            {
                var message = update.Message;

                if (message.Text != null)
                {
                    switch (message.Text.Split(' ')[0])
                    {
                        case "/start":
                            _lastAction = "/start";
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Добро пожаловать! Введите /help, чтобы узнать доступные команды.");
                            break;

                        case "/help":
                            _lastAction = "/help";
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Доступные команды:\n/help - справка\n/hello - информация о разработчике\n/inn - информация по ИНН\n/last - повторить последнее действие");
                            break;

                        case "/hello":
                            _lastAction = "/hello";
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Имя: Замостьянов Николай\nEmail: zamostyanovnikolay2003@gmail.com\nGitHub: https://github.com/KHaZzTuKBa");
                            break;

                        case "/inn":
                            _lastAction = "/inn ";
                            string[] innNumbers = message.Text.Split(' ').Skip(1).ToArray();

                            if (innNumbers.Length == 0)
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, "Введите ИНН после команды /inn и попробуйте снова");
                            }
                            else
                            {
                                foreach (var inn in innNumbers)
                                {
                                    _lastAction += inn + " ";

                                    if (Regex.IsMatch(inn, @"^\d{10}|\d{12}$"))
                                    {
                                        string companyInfo = await GetCompanyInfoByInn(inn);
                                        await botClient.SendTextMessageAsync(message.Chat.Id, companyInfo);
                                    }
                                    else
                                    {
                                        await botClient.SendTextMessageAsync(message.Chat.Id, $"ИНН {inn} некорректен.");
                                    }
                                }
                            }
                            break;

                        case "/last":
                            if (!string.IsNullOrEmpty(_lastAction))
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, $"Повтор последнего действия: {_lastAction}");
                                message.Text = _lastAction;
                                await UpdateHandler(botClient, update, cancellationToken);
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, "Предыдущее действие отсутствует.");
                            }
                            break;

                        default:
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Неизвестная команда. Введите /help для справки.");
                            break;
                    }
                }
            }
        }

        private static Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
        {
            var ErrorMessage = error switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => error.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        private static async Task<string> GetCompanyInfoByInn(string inn)
        {
            string apiKey = new StreamReader("apiKey.txt").ReadLine();
            string apiUrl = "https://suggestions.dadata.ru/suggestions/api/4_1/rs/findById/party";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Token {apiKey}");

                var requestData = new
                {
                    query = inn,
                    count = 1
                };

                var response = await client.PostAsJsonAsync(apiUrl, requestData);

                if (response.IsSuccessStatusCode)
                {
                    var responseData = await response.Content.ReadFromJsonAsync<DaDataResponse>();

                    if (responseData != null && responseData.suggestions.Any())
                    {
                        var company = responseData.suggestions.First().data;
                        return $"Информация по ИНН {inn}:\nНазвание: {company.name.short_with_opf}\nАдрес: {company.address.value}";
                    }
                    else
                    {
                        return $"По ИНН {inn} не найдена информация.";
                    }
                }
                else
                {
                    return $"Ошибка при запросе данных по ИНН {inn}: {response.ReasonPhrase}";
                }
            }
        }
    }
}
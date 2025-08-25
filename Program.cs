using System;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Net.Http.Json;
using HtmlAgilityPack;
using Fizzler.Systems.HtmlAgilityPack;
using Telegram.Bot.Args;
using System.Text.Json.Nodes;

namespace divarTlBot
{
    public class LogInResp
    {
        public string? authenticate_response { get; set; }
    }

    public class VerifyErrResp
    {
        public string? token { get; set; }
        public string? access_token { get; set; }
        public string? refresh_token{ get; set; }

        public string? type { get; set; }
        public Dictionary<string, string>? message { get; set; }
        public string? error_code { get; set; }
    }

    public class PostData
    {
        public Dictionary<string, JsonElement>? data { get; set; }
        public Dictionary<string, JsonElement>? pagination { get; set; }
        public Dictionary<string, JsonElement>? action_log { get; set; }
    }

    public class SheypoorPostData
    {
        public required int status { get; set; }
        public required List<Dictionary<string, JsonElement>> result { get; set; }

    }

    public class UpdateHandler : IUpdateHandler
    {
        public required string AuthToken { get; set; }

        public async Task<int> SendItem(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, List<PostData> data, long chatId, int count)
        {
            foreach (var item in data!)
            {
                count += 1;
                if (count % 4 == 0) break;

                string phone = "...";
                string cityPersian = "...";
                string district = "...";
                string price = "...";
                string bottomText = "...";
                string postUrl = "...";
                try
                {
                    var postsData = await Program.getPhoneNumber(authToken: AuthToken, postToken: item.data!["token"].ToString());

                    if (postsData != null)
                    {
                        string contactData = postsData[data.IndexOf(item)]["data"];

                        Match match = Regex.Match(contactData, @"""value""\s*:\s*""([0-9۰-۹]+)""");
                        phone = match.Groups[1].Value;
                    }

                    cityPersian = item.data["action"].GetProperty("payload").GetProperty("web_info").GetProperty("city_persian").ToString();
                    district = item.data["action"].GetProperty("payload").GetProperty("web_info").GetProperty("district_persian").ToString();

                    price = item.data["middle_description_text"].ToString();
                    bottomText = item.data["bottom_description_text"].ToString();

                    postUrl = await MakeUrl(item.data["title"].ToString(), item.data["token"].ToString());
                }
                catch
                {
                    Console.WriteLine("!!!cannot get data!!!");
                }

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithUrl("مشاهده در دیوار", $"{postUrl}"),
                    }
                });

                await botClient.SendPhoto(
                    chatId: chatId,
                    photo: new InputFileUrl(item.data!["image_url"].ToString()),
                    caption: $"<b>{item.data!["title"]}</b>\n{cityPersian}\n{district}\n{price}\n{bottomText}\nشماره تلفن: {phone}\n",
                    parseMode: ParseMode.Html,
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken
                );
            }
            return count;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {

            List<PostData> data = new List<PostData>();
            try
            {
                data = await Program.getData();
            }
            catch
            {
                Console.WriteLine("!!!cannot get data from source!!!");
            }

            if (update.Type == UpdateType.Message && update.Message is { } message)
            {
                string messageText = message.Text ?? string.Empty;
                long chatId = message.Chat.Id;

                Console.WriteLine($"Received message '{messageText}' from {chatId}");

                if (messageText.StartsWith("/start"))
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "👋 Hello! I’m your bot.\nTry sending /states",
                        cancellationToken: cancellationToken
                    );
                }
                else if (messageText.StartsWith("/states"))
                {
                    if (!data.Any())
                    {
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: $"<b>دقایقی دیگر تلاش کنید.</b>",
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken
                        );
                    }
                    else
                    {
                        int count = await SendItem(botClient, update, cancellationToken, data, chatId, 0);
                        var inlineKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new []
                            {
                                InlineKeyboardButton.WithCallbackData("نمایش اگهی های بیشتر", $"more {count}"),
                            }
                        });

                        await botClient.SendMessage(
                            chatId: chatId,
                            text: "برای نمایش اگهی های بیشتر کلیک کنید.",
                            replyMarkup: inlineKeyboard,
                            cancellationToken: cancellationToken
                        );
                    }
                }
                else
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "❓ I don’t understand. Try /states",
                        cancellationToken: cancellationToken
                    );
                }
            }
            else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is { } callbackQuery)
            {
                string[] callbackData = callbackQuery.Data!.Split(' ');

                if (callbackData[0] == "more")
                {
                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callbackQuery.Id,
                        text: "در حال بارگذاری..."
                    );

                    int count = Convert.ToInt32(callbackData[1].Trim());

                    count = await SendItem(botClient, update, cancellationToken, data[(count - 1)..], callbackQuery.Message!.Chat.Id, count);

                    var inlineKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData("نمایش اگهی های بیشتر", $"more {count}"),
                        }
                    });

                    await botClient.SendMessage(
                        chatId: callbackQuery.Message!.Chat.Id,
                        text: "برای نمایش اگهی های بیشتر کلیک کنید.",
                        replyMarkup: inlineKeyboard,
                        cancellationToken: cancellationToken
                    );
                }
            }
        }

        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }

        public async static Task<string> MakeUrl(string slug, string token)
        {
            string postUrl = $"https://divar.ir/v/{slug}/{token}";
            using (HttpClient client = new HttpClient())
            {
                return await client.GetStringAsync($"https://tinyurl.com/api-create.php?url={postUrl}");
            }
        }
    }

    class Program
    {
        private static List<int> lastMessageIds = new();    
        private static List<int> lastMessageIdsSheypoor = new();    

        public async static Task<List<PostData>> getData()
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.divar.ir/v8/postlist/w/search");
            request.Headers.Add("Cookie", "did=87f10c66-f25d-479e-ae0d-5b9bc7e7fc90; cdid=d85dd865-c6d4-466a-a12f-8db10c2de0da; _gcl_au=1.1.406724579.1755713615; theme=dark; _ga=GA1.1.1673344873.1755713625; multi-city=tehran%7C; city=tehran; disable_map_view=true;");
            // var content = new StringContent("{\r\n    \"city_ids\":[\"1\"],\r\n    \"search_data\":{\"form_data\":{\"data\":{\"category\":{\"str\":{\"value\":\"real-estate\"}}}}},\r\n    \"server_payload\":{\"@type\":\"type.googleapis.com/widgets.SearchData.ServerPayload\",\"additional_form_data\":{\"data\":{\"sort\":{\"str\":{\"value\":\"sort_date\"}}}}},\r\n    \"pagination_data\":{\"@type\":\"type.googleapis.com/post_list.PaginationData\",\"last_post_date\":\"2025-08-20T14:55:20.426361Z\",\"page\":1,\"layer_page\":1,\"search_uid\":\"4f6984d7-c5a9-4358-b8cf-a0d5c9bdef2e\",\"cumulative_widgets_count\":26,\"viewed_tokens\":\"H4sIAAAAAAAE/xyNUU+DMBCA/9CRaHFMHi/F1TGzzYIJ4YVcAaGkQUGKhl9vjrfL9913h2Rkd5gfAGkQuqo+AKmo7cunZGItJhEgOeXj0QBSKhrzPuwkOHy9AVL/eHqdJyYXjyvnzsZFfWKSRYnPeVC3UHDVR4HfBCDRH7XHbVdBli+AlEzXnztXzzq9m5TVZZmeWn4hy99uv6z82CGrNtbnFZBCfcQm5J2rG2QJSE7dvqvzfwAAAP//vjZCzNcAAAA=\"}\r\n}\r\n", null, "application/json");
            var content = new StringContent("{\"city_ids\":[\"1\"],\"source_view\":\"FILTER\",\"disable_recommendation\":false,\"map_state\":{\"camera_info\":{\"bbox\":{\"min_latitude\":35.604400634765625,\"min_longitude\":51.10746383666992,\"max_latitude\":35.828468322753906,\"max_longitude\":51.63630676269531},\"place_hash\":\"1||real-estate\"},\"page_state\":\"HALF_STATE\",\"interaction\":{\"list_only_used\":{}}},\"search_data\":{\"form_data\":{\"data\":{\"business-type\":{\"repeated_string\":{\"value\":[\"personal\"]}},\"category\":{\"str\":{\"value\":\"real-estate\"}}}},\"server_payload\":{\"@type\":\"type.googleapis.com/widgets.SearchData.ServerPayload\",\"additional_form_data\":{\"data\":{\"sort\":{\"str\":{\"value\":\"sort_date\"}}}}}}}", null, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string respContent = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(respContent);
            var posts = doc.RootElement.GetProperty("list_widgets").Deserialize<List<PostData>>()!;

            posts.Add(doc.RootElement.Deserialize<PostData>()!);

            // Dictionary<string, string> postInfo = new Dictionary<string, string>();
            // foreach (var post in posts!)
            // {
            //     var data = post["data"].Deserialize<Dictionary<string, JsonElement>>();
            //     JsonElement title = new JsonElement();
            //     JsonElement imgUrl = new JsonElement();
            //     data!.TryGetValue("title", out title);
            //     data!.TryGetValue("image_url", out imgUrl);
            //     postInfo.Add(title.ToString(), imgUrl.ToString());
            // }
            // return postInfo;
            return posts;
        }

        public async static Task<List<PostData>> getDataLive(string CityIds)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.divar.ir/v8/postlist/w/search");
            request.Headers.Add("accept", "application/json, text/plain, */*");
            request.Headers.Add("origin", "https://divar.ir");
            request.Headers.Add("referer", "https://divar.ir/");
            request.Headers.Add("sec-ch-ua", "\"Not;A=Brand\";v=\"99\", \"Google Chrome\";v=\"139\", \"Chromium\";v=\"139\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-site");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
            request.Headers.Add("x-render-type", "CSR");
            request.Headers.Add("x-standard-divar-error", "true");
            request.Headers.Add("X-API-Key", "{{token}}");
            var content = new StringContent($"{{\r\n    \"city_ids\": {CityIds},\r\n    \"source_view\": \"FILTER\",\r\n    \"disable_recommendation\": false,\r\n    \"map_state\": {{\r\n        \"camera_info\": {{\r\n            \"bbox\": {{\r\n                \"min_latitude\": 35.604400634765625,\r\n                \"min_longitude\": 51.10746383666992,\r\n                \"max_latitude\": 35.828468322753906,\r\n                \"max_longitude\": 51.63630676269531\r\n            }},\r\n            \"place_hash\": \"1||real-estate\"\r\n        }},\r\n        \"page_state\": \"HALF_STATE\"\r\n    }},\r\n    \"search_data\": {{\r\n        \"form_data\": {{\r\n            \"data\": {{\r\n                \"bbox\": {{\r\n                    \"repeated_float\": {{\r\n                        \"value\": [\r\n                            {{\r\n                                \"value\": 51.1074638\r\n                            }},\r\n                            {{\r\n                                \"value\": 35.6044\r\n                            }},\r\n                            {{\r\n                                \"value\": 51.6363068\r\n                            }},\r\n                            {{\r\n                                \"value\": 35.8284683\r\n                            }}\r\n                        ]\r\n                    }}\r\n                }},\r\n                \"business-type\": {{\r\n                    \"repeated_string\": {{\r\n                        \"value\": [\r\n                            \"personal\"\r\n                        ]\r\n                    }}\r\n                }},\r\n                \"recent_ads\": {{\r\n                    \"str\": {{\r\n                        \"value\": \"3h\"\r\n                    }}\r\n                }},\r\n                \"category\": {{\r\n                    \"str\": {{\r\n                        \"value\": \"real-estate\"\r\n                    }}\r\n                }}\r\n            }}\r\n        }},\r\n        \"server_payload\": {{\r\n            \"@type\": \"type.googleapis.com/widgets.SearchData.ServerPayload\",\r\n            \"additional_form_data\": {{\r\n                \"data\": {{\r\n                    \"sort\": {{\r\n                        \"str\": {{\r\n                            \"value\": \"sort_date\"\r\n                        }}\r\n                    }}\r\n                }}\r\n            }}\r\n        }}\r\n    }}\r\n}}", null, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string respContent = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(respContent);
            var posts = doc.RootElement.GetProperty("list_widgets").Deserialize<List<PostData>>()!;

            posts.Add(doc.RootElement.Deserialize<PostData>()!);

            return posts;
        }

        public static async Task<List<PostData>> TestgetDataTempRent(string CityIds)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.divar.ir/v8/postlist/w/search");
            request.Headers.Add("accept", "application/json, text/plain, */*");
            request.Headers.Add("content-type", "application/json");
            request.Headers.Add("origin", "https://divar.ir");
            request.Headers.Add("referer", "https://divar.ir/");
            request.Headers.Add("sec-ch-ua", "\"Not;A=Brand\";v=\"99\", \"Google Chrome\";v=\"139\", \"Chromium\";v=\"139\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-site");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
            request.Headers.Add("x-render-type", "CSR");
            request.Headers.Add("x-standard-divar-error", "true");
            var content = new StringContent($"{{\r\n    \"city_ids\": {CityIds},\r\n    \"source_view\":\"CATEGORY\",\"disable_recommendation\":false,\"map_state\":{{\"camera_info\":{{\"bbox\":{{}},\"page_state\":\"HALF_STATE\"}},\"search_data\":{{\"form_data\":{{\"data\":{{\"category\":{{\"str\":{{\"value\":\"temporary-rent\"}}}},\"server_payload\":{{\"@type\":\"type.googleapis.com/widgets.SearchData.ServerPayload\",\"additional_form_data\":{{\"data\":{{\"sort\":{{\"str\":{{\"value\":\"sort_date\"}}}}}}}}", null, "application/json");

            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            string respContent = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(respContent);
            var posts = doc.RootElement.GetProperty("list_widgets").Deserialize<List<PostData>>()!;

            posts.Add(doc.RootElement.Deserialize<PostData>()!);

            return posts;

        } 
        public async static Task SendItemLive(ITelegramBotClient botClient, string ChannelId, List<PostData> data, string AuthToken)
        {
            int count = 0;
            foreach (var item in data!)
            {
                if (count == 5) break;
                count += 1;

                string price = item.data!["middle_description_text"].ToString();
                string postUrl = $"https://divar.ir/v/{item.data["title"]}/{item.data["token"]}";

                string tempRent;
                try
                {
                    tempRent = item.action_log!["server_side_info"].GetProperty("info").GetProperty("extra_data").GetProperty("jli").GetProperty("category").GetProperty("value").ToString();
                    if (string.IsNullOrEmpty(tempRent)) continue;
                }
                catch (KeyNotFoundException)
                {
                    tempRent = null!;
                }

                try
                {
                    using var http = new HttpClient();
                    http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    var html = await http.GetStringAsync(postUrl);

                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var subtitleNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'kt-page-title__subtitle')]");
                    string timeLoc = subtitleNode?.InnerText.Trim() ?? "...";
                    string text =
                        $"🏡<b>{item.data!["title"]}</b>\n" +
                        $"📍{timeLoc}\n" +
                        $"💰<b>{price}</b>\n" +
                        $"🔗<a href=\"{postUrl}\">مشاهده آگهی</a>\n" +
                        $"🔗<a href=\"{item.data["image_url"]}\">مشاهده عکس</a>";
                    var sent = await botClient.SendMessage(
                        ChannelId,
                        text,
                        parseMode: ParseMode.Html,
                        linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }
                    );

                    lastMessageIds.Add(sent.MessageId);
                }
                catch
                {
                    Console.WriteLine("!!!Error Response not found");
                }
                finally
                {
                    Thread.Sleep(60000);
                }

            }
            await botClient.SendMessage(
                ChannelId,
                $"[{DateTime.Now}]"
            );
        }

        public async static Task<LogInResp> logIn(string phone)
        {
            var client = new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.divar.ir/v5/auth/authenticate");

            request.Headers.Add("Host", "api.divar.ir");
            request.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            request.Headers.Add("Sec-Ch-Ua", "\"Not;A=Brand\";v=\"99\", \"Google Chrome\";v=\"139\", \"Chromium\";v=\"139\"");
            request.Headers.Add("X-Standard-Divar-Error", "true");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("Origin", "https://divar.ir");
            request.Headers.Add("Sec-Fetch-Site", "same-site");
            request.Headers.Add("Sec-Fetch-Mode", "cors");
            request.Headers.Add("Sec-Fetch-Dest", "empty");
            request.Headers.Add("Referer", "https://divar.ir/");

            string phoneString = $"\"phone\":\"{phone}\"";
            var content = new StringContent($"{{{phoneString}}}", null, "application/x-www-form-urlencoded");
            request.Content = content;
            try
            {
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<LogInResp>() ?? new LogInResp();
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Request timed out!");
                return new LogInResp();
            }
        }

        public async static Task<VerifyErrResp> VerifyLogIn(string phone, string code)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.divar.ir/v5/auth/confirm");
            request.Headers.Add("Host", "api.divar.ir");
            request.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            request.Headers.Add("Sec-Ch-Ua", "\"Not;A=Brand\";v=\"99\", \"Google Chrome\";v=\"139\", \"Chromium\";v=\"139\"");
            request.Headers.Add("X-Standard-Divar-Error", "true");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("Origin", "https://divar.ir");
            request.Headers.Add("Sec-Fetch-Site", "same-site");
            request.Headers.Add("Sec-Fetch-Mode", "cors");
            request.Headers.Add("Sec-Fetch-Dest", "empty");
            request.Headers.Add("Referer", "https://divar.ir/");

            string verfyString = $"\"phone\":\"{phone}\",\"code\":\"{code}\"";
            var content = new StringContent($"{{{verfyString}}}", null, "application/x-www-form-urlencoded");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<VerifyErrResp>() ?? new VerifyErrResp();
        }

        public static async Task<List<Dictionary<string, string>>> getPhoneNumber(string authToken, string postToken)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.divar.ir/v8/postcontact/web/contact_info_v2/{postToken}");
            request.Headers.Add("accept", "application/json, text/plain, */*");
            request.Headers.Add("authorization", $"Basic {authToken}");
            request.Headers.Add("origin", "https://divar.ir");
            request.Headers.Add("referer", "https://divar.ir/");
            request.Headers.Add("sec-ch-ua", "\"Not;A=Brand\";v=\"99\", \"Google Chrome\";v=\"139\", \"Chromium\";v=\"139\"");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-site");
            request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");

            var content = new StringContent("{\"contact_uuid\":\"s3242523525\"}", null, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string respContent = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(respContent);

            try
            {
                var posts = doc.RootElement.GetProperty("widget_list").Deserialize<List<Dictionary<string, string>>>()!;
                return posts;
            }
            catch (KeyNotFoundException)
            {
                return null!;
            }  
        }

        public static async Task DeleteMsg(ITelegramBotClient botClient, string ChannelId, List<int> lastMessageIds)
        {
            foreach (var msgId in lastMessageIds)
            {
                try
                {
                    await botClient.DeleteMessage(ChannelId, msgId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Delete failed for {msgId}: {ex.Message}");
                }
            }

            lastMessageIds.Clear();
        }

        public static async Task SendDataIfAvailable(ITelegramBotClient botClient, string ChannelId, string AuthToken, string CityIds)
        {
            List<PostData> data = new List<PostData>();
            try
            {
                data = await getDataLive(CityIds);
            }
            catch
            {
                Console.WriteLine("!!!cannot get data from source!!!");
            }

            if (!data.Any())
            {
                Console.WriteLine($"[{DateTime.Now}] No new data available.");
            }
            else
            {
                try
                {
                    await DeleteMsg(botClient, ChannelId, lastMessageIds);

                    await SendItemLive(botClient, ChannelId, data, AuthToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        public static async Task<SheypoorPostData> SheypoorData()
        {
            string token = "0ixba6itpvyrx1u:VFcTkcQwddq4NCMvNbHh";
            SheypoorPostData postData = null!;
            while (postData == null)
                try
                {
                    var client = new HttpClient();
                    var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.majidapi.ir/sheypoor?action=category&categoryID=43604&cityID=0&page=1&token={token}");
                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    string respContent = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(respContent);
                    postData = doc.Deserialize<SheypoorPostData>()!;
                }
                catch
                {
                    Console.WriteLine("!!!cannot get sheypoor data!!! passing");
                }
            return postData;
        }

        public static async Task SendSheypoorPostData(ITelegramBotClient botClient, string ChannelId)
        {   
            SheypoorPostData postData = await SheypoorData();
            if (postData != null && postData.status == 200 && postData.result.Any())
            {
                await DeleteMsg(botClient, ChannelId, lastMessageIdsSheypoor);
                
                int count = 0;
                foreach (var item in postData.result)
                {
                    if (count == 5) break;
                    count += 1;

                    string title = item["title"].ToString();
                    string time = item["sortInfo"].ToString();
                    string price = item["priceString"].ToString();
                    string city = item["location"].GetProperty("city").ToString();
                    string region = item["location"].GetProperty("region").ToString();
                    string neighbourhood = item["location"].GetProperty("neighbourhood").ToString();
                    string isTempRent = item["shopLogo"].ToString();
                    string postURL = $"https://www.sheypoor.com/v/{item["listingID"]}";

                    // if (String.IsNullOrEmpty(isTempRent))
                    // {
                    //     string text =
                    //         $"🏡<b>{title}</b>\n" +
                    //         $"📍َُِ{time}, {region}, {city}, {neighbourhood}\n" +
                    //         $"💰<b>{price}</b>\n" +
                    //         // $"🔗<a href=\"{}\">مشاهده آگهی</a>\n" +
                    //         $"🔗<a href=\"{item["thumbImageURL"]}\">مشاهده عکس</a>";

                    //     var sent = await botClient.SendMessage(
                    //         ChannelId,
                    //         text,
                    //         parseMode: ParseMode.Html,
                    //         linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }
                    //     );

                    //     lastMessageIdsSheypoor.Add(sent.MessageId);
                    // }
                    string text =
                        $"🏡<b>{title}</b>\n" +
                        $"📍َُِ{time}, {region}, {city}, {neighbourhood}\n" +
                        $"💰<b>{price}</b>\n" +
                        $"🔗<a href=\"{postURL}\">مشاهده آگهی</a>\n" +
                        $"🔗<a href=\"{item["thumbImageURL"]}\">مشاهده عکس</a>";

                    var sent = await botClient.SendMessage(
                        ChannelId,
                        text,
                        parseMode: ParseMode.Html,
                        linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }
                    );

                    lastMessageIdsSheypoor.Add(sent.MessageId);
                }
            }
            else
            {
                Console.WriteLine("!sheypoor data not valid!");
            }
        }

        public static string getInput()
        {
            Console.WriteLine("verification code sent to your phone please enter it below");
            Console.Write("verification code: ");
            string code = Console.ReadLine()!.Trim();
            return code;
        }
        
        public async static Task Main()
        {
            Console.Write("Enter Phone Number to Login in divar: ");
            string phone = Console.ReadLine()!;

            LogInResp resp = await logIn(phone);
            if (resp.authenticate_response == null)
            {
                Console.WriteLine($"Something went wrong with Phone-Number: {phone}");
                Environment.Exit(1);
            }

            VerifyErrResp Vresp = await VerifyLogIn(phone, getInput());

            while (Vresp.token == null)
            {
                string? err;
                Vresp.message!.TryGetValue("title", out err);
                Console.WriteLine($"{err}");
                Vresp = await VerifyLogIn(phone, getInput());
            }

            string botToken = "";
            string ChannelId = "";

            var botClient = new TelegramBotClient(botToken);

            using CancellationTokenSource cts = new();

            ReceiverOptions receiverOptions = new()
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            string CityIds = "";
            using (StreamReader reader = new StreamReader("CityIds.txt"))
            {
                CityIds = reader.ReadToEnd().ToString();
            }

            Timer timer = new Timer(async _ =>
            {
                try
                {
                    await SendSheypoorPostData(botClient, ChannelId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Timer2 Error: {ex.Message}");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));

            Timer timer2 = new Timer(async _ =>
            {
                try
                {
                    await SendDataIfAvailable(botClient, ChannelId, Vresp.token, CityIds);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Timer1 Error: {ex.Message}");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));

            botClient.StartReceiving(
                new UpdateHandler()
                {
                    AuthToken = Vresp.token
                },
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            var me = await botClient.GetMe();
            Console.WriteLine($"Bot started: @{me.Username}");
            Console.ReadLine();

            cts.Cancel();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RallyLookbackExample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string bearerToken = "_uAbjpTDQJWTVeD5GtKcmuOj2mjeO0FrqOMyAi1HgSM"; // e.g. "_abcd1234..."
            string workspaceOid = "65088890229"; // e.g. "12345678910"
            long featureOid = 80035780277;                     // Replace with the Feature ObjectID

            GetFeatureStateChanges(bearerToken, workspaceOid, featureOid);
        }

        public static HttpClient WebAuthenticationWithToken()
        {
            HttpClient confClient = new HttpClient();

            try
            {
                confClient.DefaultRequestHeaders.Add("Authorization", "Bearer ");
                confClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                confClient.Timeout = TimeSpan.FromMilliseconds(100000000);
            }
            catch (Exception)
            {
                // Handle exception
            }

            return confClient;
        }

        public static string WebPostWithToken(HttpClient confClient, string url, string payload)
        {
            string json = string.Empty;
            bool tryagain = true;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 |
                                                   SecurityProtocolType.Tls11 |
                                                   SecurityProtocolType.Tls;

            int nbRetry = 3; // Convert.ToInt32(ConfigurationManager.AppSettings["nbRetry"] ?? "3");

            while (tryagain)
            {
                try
                {
                    if (nbRetry > 0)
                    {
                        HttpContent content = new StringContent("{\n              \"find\": {\n                \"ObjectID\": 74266610813\n              },\n              \"fields\": [\"State\", \"ScheduleState\",\"_ValidFrom\", \"_ValidTo\", \"ObjectID\", \"FormattedID\"],\n              \"hydrate\":[\"State\",\"ScheduleState\"],\n              \"compress\": false,\n              \"pagesize\": 2000\n            }");
    
                        HttpResponseMessage message = confClient.PostAsync(url, content).Result;

                        if (message.IsSuccessStatusCode)
                        {
                            json = message.Content.ReadAsStringAsync().Result;
                            if (!json.TrimStart().StartsWith("<"))
                            {
                                tryagain = false;
                                return json;
                            }
                        }
                        else
                        {
                            Console.WriteLine(message.ReasonPhrase);
                            nbRetry--;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Http Response error - Retries exhausted. Stopping.");
                        Environment.Exit(-1);
                    }
                }
                catch (Exception e)
                {
                    if (nbRetry > 0)
                    {
                        Console.WriteLine("Http Response error: {0} - retrying ({1} left)", e.Message, nbRetry);
                        nbRetry--;
                    }
                    else
                    {
                        Console.WriteLine("Http Response error - Retries exhausted. Stopping.");
                        Environment.Exit(-1);
                    }
                }
            }

            return json;
        }

        public static void GetFeatureStateChanges(string bearerToken, string workspaceOid, long featureOid)
        {
            string url = $"https://eu1.rallydev.com/analytics/v2.0/service/rally/workspace/{workspaceOid}/artifact/snapshot/query.js";

            HttpClient confClient = WebAuthenticationWithToken();
            confClient.DefaultRequestHeaders.Remove("Authorization");
            confClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + bearerToken);

            string payload = @"
            {
              ""find"": {
                ""ObjectID"": 74999836977
                }
              },
              ""fields"": [""State"", ""_ValidFrom"", ""_ValidTo"", ""ObjectID"", ""FormattedID"",""_PreviousValues.State""],
              ""hydrate"":[""State""],
              ""compress"": true,
              ""start"": 0,
              ""pagesize"": 2000
            }";

            //""fields"": [""ObjectID"", ""_ValidFrom"", ""_ValidTo"", ""State"",""schedulestate"",""_PreviousValues.State""],

            string responseJson = WebPostWithToken(confClient, url, payload);

            if (!string.IsNullOrEmpty(responseJson))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                };

                var response = JsonSerializer.Deserialize<LookbackResponse>(responseJson, options);
                if (response != null && response.Results != null)
                {
                    foreach (var item in response.Results)
                    {
                        Console.WriteLine($"Feature {item.ObjectID} changed state from '{item.PreviousValues}' to '{item.State}' at {item.ValidFrom}");
                    }
                }
                else
                {
                    Console.WriteLine("No state changes found.");
                }
            }

            Console.WriteLine("hit a key...");
            Console.ReadKey();
        }
    }

    // Response Models
    public class LookbackResponse
    {
        [JsonPropertyName("Results")]
        public List<LookbackResult> Results { get; set; }
    }

    public class LookbackResult
    {
        [JsonPropertyName("ObjectID")]
        public long ObjectID { get; set; }

        [JsonPropertyName("_ValidFrom")]
        public DateTime ValidFrom { get; set; }

        [JsonPropertyName("_ValidTo")]
        public DateTime ValidTo { get; set; }

        [JsonPropertyName("State")]
        public string State { get; set; }

        [JsonPropertyName("_PreviousValues")]
        public PreviousValues PreviousValues { get; set; }
    }

    public class PreviousValues
    {
        [JsonPropertyName("State")]
        public string State { get; set; }
    }
}

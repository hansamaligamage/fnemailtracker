using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Web;
using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Cosmos;

namespace fnemailtracker
{
    public static class ReadInbox
    {
        [FunctionName("ReadInbox")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
           
            var response = await ProcessWebhookNotificationsAsync(req, log, async hook =>
            {
                return await CheckForSubscriptionChangesAsync(hook.SubscriptionId, hook.Resource, log);
            });

            return response;
        }

        private static async Task<HttpResponseMessage> ProcessWebhookNotificationsAsync(HttpRequestMessage req, ILogger log,
            Func<SubscriptionNotification, Task<bool>> processSubscriptionNotification)
        {
            // Read the body of the request and parse the notification
            string content = await req.Content.ReadAsStringAsync();
            log.LogInformation($"Raw request content: {content}");

            var webhooks = JsonConvert.DeserializeObject<WebhookNotification>(content);
            if (webhooks?.Notifications != null)
            {
                foreach (var hook in webhooks.Notifications)
                {
                    log.LogInformation($"Hook received for subscription: '{hook.SubscriptionId}' " +
                        $"Resource: '{hook.Resource}', changeType: '{hook.ChangeType}'");
                    try
                    {
                        await processSubscriptionNotification(hook);
                    }
                    catch (Exception ex)
                    {
                        log.LogError($"Error processing subscription notification. Subscription {hook.SubscriptionId}" +
                            $" was skipped. {ex.Message}", ex);
                    }
                }
                return req.CreateResponse(HttpStatusCode.NoContent);
            }
            else
            {
                log.LogInformation($"Request was incorrect. Returning bad request.");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }
        }

        private static async Task<bool> CheckForSubscriptionChangesAsync(string subscriptionId, string resource, ILogger log)
        {
            bool success = false;

            //Get access token from configuration
            string accessToken = System.Environment.GetEnvironmentVariable("AccessToken", EnvironmentVariableTarget.Process);
            log.LogInformation($"accessToken: {accessToken}");

            HttpClient client = new HttpClient();

            // Send Graph request to fetch mail
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/" + resource);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(continueOnCapturedContext: false);

            log.LogInformation("Outlook API response " + response.ToString());

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();

                JObject obj = (JObject)JsonConvert.DeserializeObject(result);

                string subject = (string)obj["subject"];

                log.LogInformation($"Subject : {subject}");

                string content = (string)obj["body"]["content"];

                log.LogInformation($"Email Body : {content}");

                string database = "mailstore";
                string container = "emails";

                CosmosClient cosmosClient = DBConnection.CreateCosmosClient(log);
                await DBConnection.CreateDatabaseAsync(cosmosClient, database, log);
                await DBConnection.CreateContainerAsync(cosmosClient, database, container, log);
                await DBConnection.AddItemsToContainerAsync(cosmosClient, database, container, new Email { Id = Guid.NewGuid(), Subject = subject, EmailBody = content }, log);
                success = true;
            }

            return success;
        }

        private static bool GetValidationToken(HttpRequestMessage req, out string token)
        {
            var query = req.RequestUri.Query;

            var tokenstring = HttpUtility.ParseQueryString(query);
            token = tokenstring[0];
            return !string.IsNullOrEmpty(token);
        }

        private static HttpResponseMessage PlainTextResponse(string text)
        {
            HttpResponseMessage response = new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(text, System.Text.Encoding.UTF8, "text/plain")
            };
            return response;
        }
    }
}

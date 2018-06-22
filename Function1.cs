using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SendEmailTrigger
{
    public static class Function1
    {

        private const string idaMicrosoftGraphUrl = "https://graph.microsoft.com";

        [FunctionName("EmailTrigger")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req,
            TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            string validationToken;
            if (GetValidationToken(req, out validationToken))
            {
                return PlainTextResponse(validationToken);
            }

            //Process each notification
            var response = await ProcessWebhookNotificationsAsync(req, log, async hook =>
            {
                return await CheckForSubscriptionChangesAsync(hook.Resource, log);
            });
            return response;
        }



        private static async Task<HttpResponseMessage> ProcessWebhookNotificationsAsync(HttpRequestMessage req, TraceWriter log, 
            Func<SubscriptionNotification, Task<bool>> processSubscriptionNotification)
        {

            // Read the body of the request and parse the notification
            string content = await req.Content.ReadAsStringAsync();
            log.Verbose($"Raw request content: {content}");

            var webhooks = JsonConvert.DeserializeObject<WebhookNotification>(content);

            if (webhooks?.Notifications != null)
            {
                // Since webhooks can be batched together, loop over all the notifications we receive and process them separately.
                foreach (var hook in webhooks.Notifications)
                {
                    log.Info($"Hook received for subscription: '{hook.SubscriptionId}' Resource: '{hook.Resource}', changeType: '{hook.ChangeType}'");
                    try
                    {
                        await processSubscriptionNotification(hook);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error processing subscription notification. Subscription {hook.SubscriptionId} was skipped. {ex.Message}", ex);
                    }
                }

                // After we process all the messages, return an empty response.
                return req.CreateResponse(HttpStatusCode.NoContent);
            }
            else
            {
                log.Info($"Request was incorrect. Returning bad request.");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }
        }

        private static async Task<bool> CheckForSubscriptionChangesAsync(string resource, TraceWriter log)
        {

            bool success = false;

            // Get access token from configuration
            string accessToken = System.Environment.GetEnvironmentVariable("AccessToken", EnvironmentVariableTarget.Process);
            log.Info($"accessToken: {accessToken}");

            HttpClient client = new HttpClient();

            // Send Graph request to fetch mail
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/" + resource);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(continueOnCapturedContext: false);

            log.Info(response.ToString());

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();

                JObject obj = (JObject)JsonConvert.DeserializeObject(result);

                string subject = (string)obj["subject"];

                log.Verbose($"Subject : {subject}");

                string content = (string)obj["body"]["content"];

                log.Verbose($"Email Body : {content}");

                success = true;
            }

            return success;
        }

        private static bool GetValidationToken(HttpRequestMessage req, out string token)
        {
            var query = req.RequestUri.Query;

            var tokenstring = HttpUtility.ParseQueryString(query);
            token = tokenstring["validationToken"];
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

        private static async Task<string> RetrieveAccessTokenAsync(TraceWriter log)
        {

            log.Verbose($"Retriving new accessToken");

            string authorityUrl = System.Environment.GetEnvironmentVariable("AuthorityUrl", EnvironmentVariableTarget.Process);

            var authContext = new AuthenticationContext(authorityUrl);

            string clientId = System.Environment.GetEnvironmentVariable("ClientId", EnvironmentVariableTarget.Process);
            string clientSecret = System.Environment.GetEnvironmentVariable("ClientSecret", EnvironmentVariableTarget.Process);

            try
            {
                var clientCredential = new ClientCredential(clientId, clientSecret);
                var authResult = await authContext.AcquireTokenAsync(idaMicrosoftGraphUrl, clientCredential);
                return authResult.AccessToken;
            }
            catch (AdalException ex)
            {
                log.Info($"ADAL Error: Unable to retrieve access token: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                log.Info(ex.Message);
            }

            return null;
        }

    private class SubscriptionNotification
    {
        [JsonProperty("clientState")]
        public string ClientState { get; set; }
        [JsonProperty("resource")]
        public string Resource { get; set; }
        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; }
        [JsonProperty("changeType")]
        public string ChangeType { get; set; }
    }

        private class WebhookNotification
        {
            [JsonProperty("value")]
            public SubscriptionNotification[] Notifications { get; set; }
        }

    }
}

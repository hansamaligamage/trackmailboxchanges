This sample code is going to describe how to create a function app on .NET Core and publish it into Azure, this function app url is going to act as a notification url. 

In Graph API, we are going to use subscription API, to subscribe to SentItems folder in mail box, whenever we are going to send an email, function app gets notification about the changes and mail details.

Used Visual Studio 2017 with .NET Core 2.0 version to develop the solution

Run method in Azure Function

```
[FunctionName("EmailTrigger")] 
public static async Task Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", 
 Route = null)]HttpRequestMessage req, TraceWriter log) 
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
```

Process email box chanegs, email notification is parsed into an object for later use

```
private static async Task ProcessWebhookNotificationsAsync(HttpRequestMessage req, TraceWriter log, Func> processSubscriptionNotification) 
{ 
  // Read the body of the request and parse the notification 
  string content = await req.Content.ReadAsStringAsync(); 
  log.Verbose($"Raw request content: {content}"); 
  
  var webhooks = JsonConvert.DeserializeObject(content); 
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
```

Extract the required details in each email, subject & body parameters

```
private static async Task CheckForSubscriptionChangesAsync(string resource, TraceWriter log) 
{ 
 bool success = false; 
  
 // Obtain an access token 
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
```

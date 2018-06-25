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


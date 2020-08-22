# Http trigger in .NET Core to read mails from MS Graph API and store them in CosmosDB using SQL API

This is a http trigger function written in C# - .NET Core 3.1. It reads emails from your mail account using Microsoft Graph API and store data to Cosmos DB on SQL API

## Technology stack  
* .NET Core 3.1 on Visual Studio 2019
* Azure functions v3 and Azure Cosmos DB SQL API

## How to run the solution
 * At first create a subscription to get the notification when an email recieved in your inbox, check the Microsoft Graph Subscription API to get a better understanding, https://docs.microsoft.com/en-us/graph/api/subscription-post-subscriptions
 * You have to create a new application in the active directory, then obtain a secret, provide mail read permission and generate an access token to read the emails landed to the inbox, check this blog if you want to know more information, http://hansamaligamage.blogspot.com/2018/06/net-core-app-that-talks-with-graph-api.html
 * You have to create a Cosmos DB account with SQL API then go to the Keys section, get the endpoint url and keys to connect to the database
 * Open the solution file in Visual Studio and build the project
 
## Code snippets
### HttpTrigger entry point to the function app
```
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
  ```
  
### Get the email notifications
```
private static async Task<HttpResponseMessage> ProcessWebhookNotificationsAsync(HttpRequestMessage req,
  ILogger log, Func<SubscriptionNotification, Task<bool>> processSubscriptionNotification)
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
                log.LogError($"Error processing subscription notification. Subscription {hook.SubscriptionId}" 
                  + $" was skipped. {ex.Message}", ex);
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
```

## Process and store email body by passing the email access token
```
private static async Task<bool> CheckForSubscriptionChangesAsync(string subscriptionId, string resource
  , ILogger log)
{
    bool success = false;

    //Get access token from configuration
    string accessToken = System.Environment.GetEnvironmentVariable("AccessToken"
      , EnvironmentVariableTarget.Process);
    log.LogInformation($"accessToken: {accessToken}");

     HttpClient client = new HttpClient();

     // Send Graph request to fetch mail
     HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, 
        "https://graph.microsoft.com/v1.0/" + resource);
     request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

     HttpResponseMessage response = await client.SendAsync(request)
       .ConfigureAwait(continueOnCapturedContext: false);
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
        await DBConnection.AddItemsToContainerAsync(cosmosClient, database, container, 
            new Email { Id = Guid.NewGuid(), Subject = subject, EmailBody = content }, log);
        success = true;
    }
return success;
}
```

### Create Cosmos DB client
```
public static CosmosClient CreateCosmosClient(ILogger log)
{
    var endpointUrl = GetEndpointUrl();
    var authorizationKey = GetKey();
    log.LogInformation("Cosmos DB account is created\n");
    return new CosmosClient(endpointUrl, authorizationKey);
}
```

### Create database in Cosmos DB account
```
public static async Task CreateDatabaseAsync(CosmosClient cosmosClient, string database, ILogger log)
{
    // Create a new database
    var cosmosdb = await cosmosClient.CreateDatabaseIfNotExistsAsync(database);
    log.LogInformation("Created Database: {0}\n", database);
}
```

### Create container in the database
```
public static async Task CreateContainerAsync(CosmosClient cosmosClient, string database, string container,
  ILogger log)
{
    // Create a new container
    var cosmoscontainer = await cosmosClient.GetDatabase(database).CreateContainerIfNotExistsAsync(container
      , "/Subject");
    log.LogInformation("Created Container: {0}\n", container);
 }
```

### Create items in container
```
public static async Task AddItemsToContainerAsync(CosmosClient cosmosClient, string database, string container
, Email email  , ILogger log)
 {
    var cosmosContainer = cosmosClient.GetContainer(database, container);
    try
    {
        ItemResponse<Email> emailResponse = await cosmosContainer.CreateItemAsync<Email>(email,
          new PartitionKey(email.Subject));
    }
    catch(Exception ex)
    {
        log.LogInformation("Error " + ex.GetBaseException());
    }
    log.LogInformation("Created item in database with id: {0}\n", email.Id);
}
```

using Azure.Messaging;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;

namespace AdtEventEndpoint
{
    public static class AdtEventEndpoint
    {
        [FunctionName("negotiate")]
        public static SignalRConnectionInfo GetSignalRInfo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [SignalRConnectionInfo(HubName = "dt-event-endpoint-hub")] SignalRConnectionInfo connectionInfo)
        {
            return connectionInfo;
        }

        [FunctionName("broadcast")]
        public static Task Observe(
            [EventGridTrigger] CloudEvent eventGridEvent,
            [SignalR(HubName = "dt-event-endpoint-hub", ConnectionStringSetting = "AzureSignalRConnectionString")] IAsyncCollector<SignalRMessage> signalRMessages,
            ILogger log)
        {
            JObject message = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
            message.Add("id", eventGridEvent.Subject);
            message.Add("eventType", eventGridEvent.Type);
            message.Add("eventDateTime", eventGridEvent.Time.ToString());
            log.LogInformation($"New event:\n {message.ToString()}");

            return signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "newMessage",
                    Arguments = new[] { JsonConvert.SerializeObject(message) }
                });
        }
    }
}

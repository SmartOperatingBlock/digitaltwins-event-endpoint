using System;
using Azure.Messaging;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdtEventEndpoint
{
    public static class AdtEventEndpoint
    {
        [FunctionName("broadcast")]
        public static void Observe(
            [EventGridTrigger] CloudEvent eventGridEvent,
            ILogger log)
        {
            JObject message = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
            message.Add("id", eventGridEvent.Subject);
            message.Add("eventType", eventGridEvent.Type);
            message.Add("eventDateTime", eventGridEvent.Time.ToString());
            log.LogInformation($"Reading event: {message.ToString()}");
        }
    }
}

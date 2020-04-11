using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AzureEventGridSimulator.Extensions;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Controllers
{
    [Route("/api/events")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ILogger _logger;

        public ApiController(ILogger logger)
        {
            _logger = logger;
        }

        public TopicSettings TopicSettings => HttpContext.RetrieveTopicSettings();

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            var events = HttpContext.RetrieveEvents();

            //_logger.LogInformation($"New request ({events.Length} event(s)) for '{TopicSettings.Name}' @ {Request.GetDisplayUrl()}");
            foreach (var gridEvent in events)
            {
                using (_logger.BeginScope("New event"))
                {
                    Colorful.Console.WriteLine($"New event: Topic:{TopicSettings.Name}, Subject:{gridEvent.Subject}", Color.Coral);

                    if (TopicSettings.ShowFullEventTrace)
                    {
                        Colorful.Console.WriteLine($"Event:{JsonConvert.SerializeObject(gridEvent, Formatting.Indented)}", Color.Aqua);
                    }

                    if (TopicSettings?.Subscribers?.Any() == true)
                    {
                        foreach (var subscription in TopicSettings.Subscribers)
                        {
                            await SendToSubscriber(subscription, gridEvent);
                        }
                    }
                    else
                    {
                        if (TopicSettings.SaveEventsToTempFolder)
                        {
                            var directory = $"{Path.GetTempPath()}AzureEventGridSimulator";
                            var filePath = $"{directory}\\{TopicSettings.Name}-{gridEvent.Subject}-{DateTime.UtcNow.Ticks}.json";

                            if (!Directory.Exists(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }

                            await System.IO.File.WriteAllTextAsync(filePath, JsonConvert.SerializeObject(gridEvent, Formatting.Indented));
                        }
                    }
                }
            }

            return Ok();
        }

        private async Task SendToSubscriber(SubscriptionSettings subscription, params EventGridEvent[] events)
        {
            try
            {
                _logger.LogDebug("Sending to subscriber '{SubscriberName}'.", subscription.Name);

                // "Event Grid sends the events to subscribers in an array that has a single event. This behavior may change in the future."
                // https://docs.microsoft.com/en-us/azure/event-grid/event-schema
                foreach (var evt in events)
                {
                    var json = JsonConvert.SerializeObject(new[] { evt }, Formatting.Indented);
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Add("aeg-event-type", "Notification");
                        httpClient.Timeout = TimeSpan.FromSeconds(5);

                        await httpClient.PostAsync(subscription.Endpoint, content)
                                        .ContinueWith(t =>
                                        {
                                            if (t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode)
                                            {
                                                _logger.LogDebug("Event {EventId} sent to subscriber '{SubscriberName}' successfully.", evt.Id, subscription.Name);
                                            }
                                            else
                                            {
                                                _logger.LogError(t.Exception?.GetBaseException(),
                                                                 "Failed to send event {EventId} to subscriber '{SubscriberName}', '{TaskStatus}', '{Reason}'.", evt.Id,
                                                                 subscription.Name,
                                                                 t.Status.ToString(),
                                                                 t.Result?.ReasonPhrase);
                                            }
                                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send to subscriber '{SubscriberName}'.", subscription.Name);
            }
        }
    }
}

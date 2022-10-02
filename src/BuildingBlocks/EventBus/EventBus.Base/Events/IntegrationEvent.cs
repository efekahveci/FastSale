using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EventBus.Base.Events;

public class IntegrationEvent
{
    [JsonPropertyName("ID")]
    public Guid Id { get; private set; }
    [JsonPropertyName("DateTime")]

    public DateTime CreatedDate { get; private set; }


    public IntegrationEvent()
    {
        Id = Guid.NewGuid();
        CreatedDate = DateTime.UtcNow;
    }
    [JsonConstructor]
    public IntegrationEvent(Guid guid, DateTime dateTime)
    {
        Id = guid;
        CreatedDate = dateTime; 
    }
}

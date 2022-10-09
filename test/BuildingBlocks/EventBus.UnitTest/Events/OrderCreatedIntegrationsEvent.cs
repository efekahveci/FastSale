using EventBus.Base.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.UnitTest.Events;

public class OrderCreatedIntegrationsEvent:IntegrationEvent
{
    public int Id { get; set; }

	public OrderCreatedIntegrationsEvent(int id)
	{
		Id = id;	
	}
}

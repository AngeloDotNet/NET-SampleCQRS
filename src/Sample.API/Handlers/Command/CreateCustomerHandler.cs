using System.Text.Json;
using MediatR;
using Sample.API.Data;
using Sample.API.Entities;
using Sample.API.Events;
using Sample.API.Models.Command;

namespace Sample.API.Handlers.Command;

public class CreateCustomerHandler(WriteDbContext writeDb) : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Convert DTO -> Entity
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow
        };

        // Create event payload
        var evt = new CustomerCreatedEvent
        {
            EventId = Guid.NewGuid(),
            AggregateId = customer.Id,
            OccurredAt = DateTime.UtcNow,
            Data = JsonSerializer.Serialize((customer.Id, customer.Name, customer.Email))
        };

        // Persist entity, event store and outbox in the SAME DbContext/Transaction
        writeDb.Customers.Add(customer);

        writeDb.EventStore.Add(new EventStoreEntry
        {
            EventId = evt.EventId,
            AggregateId = evt.AggregateId,
            Type = nameof(CustomerCreatedEvent),
            Data = evt.Data,
            OccurredAt = evt.OccurredAt
        });

        writeDb.OutboxMessages.Add(new OutboxMessage
        {
            MessageId = evt.EventId, // reuse EventId as message id for idempotency
            Type = nameof(CustomerCreatedEvent),
            Data = JsonSerializer.Serialize(evt),
            OccurredAt = evt.OccurredAt
        });

        await writeDb.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}
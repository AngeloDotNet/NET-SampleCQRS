using MediatR;

namespace Sample.API.Models.Command;

public record CreateCustomerCommand(string Name, string Email) : IRequest<Guid>;
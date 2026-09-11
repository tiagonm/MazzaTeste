using MediatR;
using Mazza.Orders.Application.Orders.Dtos;

namespace Mazza.Orders.Application.Orders.Commands.CreateOrder;

/// <summary>
/// Places a new order for a customer.
/// </summary>
/// <param name="CustomerId">Customer the order belongs to.</param>
/// <param name="Items">Lines to place - at least one is required.</param>
public sealed record CreateOrderCommand(
    Guid CustomerId,
    IReadOnlyList<CreateOrderItemRequest> Items) : IRequest<OrderDto>;

/// <summary>
/// One requested line of a <see cref="CreateOrderCommand"/>.
/// </summary>
/// <param name="ProductName">Product description.</param>
/// <param name="Quantity">Units to order.</param>
/// <param name="UnitPrice">Price per unit.</param>
public sealed record CreateOrderItemRequest(string ProductName, int Quantity, decimal UnitPrice);

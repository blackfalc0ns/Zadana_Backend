using MediatR;

namespace Zadana.Application.Modules.Catalog.Commands.Categories.DeleteCategory;

public record DeleteCategoryCommand(Guid Id) : IRequest;

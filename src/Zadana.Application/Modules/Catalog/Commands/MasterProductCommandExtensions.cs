using Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;
using Zadana.Application.Modules.Catalog.Commands.UpdateMasterProduct;

namespace Zadana.Application.Modules.Catalog.Commands;

internal static class MasterProductCommandExtensions
{
    public static Guid? ResolveMeasurementUnitId(this CreateMasterProductCommand command) =>
        command.MeasurementUnitId ?? command.UnitId;

    public static Guid? ResolveMeasurementUnitId(this UpdateMasterProductCommand command) =>
        command.MeasurementUnitId ?? command.UnitId;
}

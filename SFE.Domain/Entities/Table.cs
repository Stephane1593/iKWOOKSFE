using SFE.Domain.Common;

namespace SFE.Domain.Entities;

public enum TableStatus { Free, Occupied, Reserved, Cleaning }

public class Table : SyncableEntity
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }

    public int Number { get; set; }    // table number, unique per restaurant
    public int Seats { get; set; }
    public TableStatus Status { get; set; } = TableStatus.Free;
}